using System.Diagnostics;
using System.Threading.RateLimiting;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Activation;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagment;
using GIMI_ModManager.WinUI.Services.AppManagment.Updating;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.Views;
using GIMI_ModManager.WinUI.Views.CharacterManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Polly;
using Polly.RateLimiting;
using Polly.Retry;
using Serilog;
using Serilog.Events;
using Serilog.Templates;

namespace GIMI_ModManager.WinUI;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host { get; }

    public static T GetService<T>()
        where T : class
    {
        if ((Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");

        return service;
    }

    public static string TMP_DIR { get; } = Path.Combine(Path.GetTempPath(), "JASM_TMP");
    public static string ROOT_DIR { get; } = AppDomain.CurrentDomain.BaseDirectory;
    public static string ASSET_DIR { get; } = Path.Combine(ROOT_DIR, "Assets");

    public static WindowEx MainWindow { get; } = new MainWindow();
    public static UIElement? AppTitlebar { get; set; }

    public static bool OverrideShutdown { get; set; }
    public static bool UnhandledExceptionHandled { get; set; }

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseSerilog((context, configuration) =>
            {
                configuration.MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);

                configuration.Filter.ByExcluding(logEvent =>
                    logEvent.Exception is RateLimiterRejectedException);
                configuration.Enrich.FromLogContext();


                configuration.ReadFrom.Configuration(context.Configuration);
                var mt = new ExpressionTemplate(
                    "[{@t:yyyy-MM-dd'T'HH:mm:ss} {@l:u3} {Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}] {@m}\n{@x}");
                configuration.WriteTo.File(formatter: mt, "logs\\log.txt");
                if (Debugger.IsAttached) configuration.WriteTo.Debug();
            })
            .ConfigureServices((context, services) =>
            {
                // Default Activation Handler
                services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

                // Other Activation Handlers
                services.AddTransient<IActivationHandler, FirstTimeStartupActivationHandler>();

                // Services
                services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
                services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
                services.AddSingleton<INavigationViewService, NavigationViewService>();

                services.AddSingleton<IActivationService, ActivationService>();
                services.AddSingleton<IPageService, PageService>();
                services.AddSingleton<INavigationService, NavigationService>();

                services.AddSingleton<IWindowManagerService, WindowManagerService>();
                services.AddSingleton<NotificationManager>();
                services.AddSingleton<ModNotificationManager>();
                services.AddTransient<ModDragAndDropService>();

                services.AddSingleton<ElevatorService>();
                services.AddSingleton<GenshinProcessManager>();
                services.AddSingleton<ThreeDMigtoProcessManager>();

                services.AddSingleton<UpdateChecker>();
                services.AddSingleton<AutoUpdaterService>();

                services.AddSingleton<ImageHandlerService>();
                services.AddSingleton<SelectedGameService>();

                // Core Services
                services.AddSingleton<IFileService, FileService>();
                services.AddSingleton<IGameService, GameService>();
                services.AddSingleton<ISkinManagerService, SkinManagerService>();
                services.AddSingleton<ModCrawlerService>();
                services.AddSingleton<ModSettingsService>();
                services.AddSingleton<KeySwapService>();
                services.AddSingleton<ILanguageLocalizer, Localizer>();

                services.AddSingleton<GameBananaCache>();
                services.AddTransient<IModUpdateChecker, GameBananaChecker>();

                // Even though I've followed the docs, I keep getting "Exception thrown: 'System.IO.IOException' in System.Net.Sockets.dll"
                // I've read just about every microsoft docs page httpclients, and I can't figure out what I'm doing wrong
                // Also tried with httpclientfactory, but that didn't work either
                services.AddHttpClient<IModUpdateChecker, GameBananaChecker>(client =>
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "JASM-Just_Another_Skin_Manager-Update-Checker");
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                    })
                    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler()
                    {
                        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
                    })
                    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

                // I'm preeeetty sure this is not correctly set up, not used to polly 8.x.x
                // But it does rate limit, so I guess it's fine for now
                services.AddResiliencePipeline(GameBananaChecker.HttpClientName, (builder, context) =>
                {
                    var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions()
                    {
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        TokenLimit = 10,
                        AutoReplenishment = true,
                        TokensPerPeriod = 2,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(2)
                    });

                    builder
                        .AddRateLimiter(limiter)
                        .AddRetry(new RetryStrategyOptions()
                        {
                            BackoffType = DelayBackoffType.Linear,
                            UseJitter = true,
                            MaxRetryAttempts = 8,
                            Delay = TimeSpan.FromMilliseconds(200)
                        });

                    builder.TelemetryListener = null;
                    context.OnPipelineDisposed(() =>
                    {
                        // This is never called, so I'm not sure if this is correct
                        Log.Debug("Disposing rate limiter");
                        limiter.Dispose();
                    });
                });
                services.AddSingleton<ModUpdateAvailableChecker>();

                // Views and ViewModels
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SettingsPage>();
                services.AddTransient<StartupViewModel>();
                services.AddTransient<StartupPage>();
                services.AddTransient<ShellPage>();
                services.AddTransient<ShellViewModel>();
                services.AddTransient<NotificationsViewModel>();
                services.AddTransient<NotificationsPage>();
                services.AddTransient<CharactersViewModel>();
                services.AddTransient<CharactersPage>();
                services.AddTransient<CharacterDetailsViewModel>();
                services.AddTransient<CharacterDetailsPage>();
                services.AddTransient<DebugViewModel>();
                services.AddTransient<DebugPage>();
                services.AddTransient<CharacterManagerViewModel>();
                services.AddTransient<CharacterManagerPage>();
                services.AddTransient<EditCharacterViewModel>();
                services.AddTransient<EditCharacterPage>();

                // Configuration
                services.Configure<LocalSettingsOptions>(
                    context.Configuration.GetSection(nameof(LocalSettingsOptions)));
            }).Build();

        UnhandledException += App_UnhandledException;
    }

    private async void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled exception");
        await Log.CloseAndFlushAsync();

        if (UnhandledExceptionHandled)
            return;
        // show error dialog
        var window = new ErrorWindow(e.Exception)
        {
            IsAlwaysOnTop = true,
            Title = "JASM - Unhandled Exception",
            SystemBackdrop = new MicaBackdrop()
        };
        window.Activate();
        window.CenterOnScreen();
        MainWindow.Hide();
        e.Handled = true;
        UnhandledExceptionHandled = true;
    }


    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Environment.SetEnvironmentVariable("WEBVIEW2_USE_VISUAL_HOSTING_FOR_OWNED_WINDOWS", "1");
        await GetService<ILanguageLocalizer>().InitializeAsync();
        NotImplemented.NotificationManager = GetService<NotificationManager>();
        base.OnLaunched(args);
        await GetService<IActivationService>().ActivateAsync(args).ConfigureAwait(false);
    }
}