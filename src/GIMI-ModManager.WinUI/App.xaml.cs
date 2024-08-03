using System.Diagnostics;
using System.Threading.RateLimiting;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.Core.Services.GameBanana;
using GIMI_ModManager.Core.Services.ModPresetService;
using GIMI_ModManager.WinUI.Activation;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.AppManagement.Updating;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;
using GIMI_ModManager.WinUI.ViewModels.SettingsViewModels;
using GIMI_ModManager.WinUI.Views;
using GIMI_ModManager.WinUI.Views.CharacterManager;
using GIMI_ModManager.WinUI.Views.Settings;
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
using GameBananaService = GIMI_ModManager.WinUI.Services.ModHandling.GameBananaService;
using NotificationManager = GIMI_ModManager.WinUI.Services.Notifications.NotificationManager;

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

    public static DirectoryInfo GetUniqueTmpFolder()
    {
        var tmpFolder = new DirectoryInfo(Path.Combine(TMP_DIR, Guid.NewGuid().ToString()));
        if (!tmpFolder.Exists)
            tmpFolder.Create();
        return tmpFolder;
    }

    public static string TMP_DIR { get; } = Path.Combine(Path.GetTempPath(), "JASM_TMP");
    public static string ROOT_DIR { get; } = AppDomain.CurrentDomain.BaseDirectory;
    public static string ASSET_DIR { get; } = Path.Combine(ROOT_DIR, "Assets");

    public static WindowEx MainWindow { get; } = new MainWindow();
    public static UIElement? AppTitlebar { get; set; }

    public static bool OverrideShutdown { get; set; }
    public static bool IsShuttingDown { get; set; }
    public static bool ShutdownComplete { get; set; }

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
                services.AddSingleton<CharacterSkinService>();

                services.AddSingleton<ElevatorService>();
                services.AddSingleton<GenshinProcessManager>();
                services.AddSingleton<ThreeDMigtoProcessManager>();

                services.AddSingleton<UpdateChecker>();
                services.AddSingleton<AutoUpdaterService>();

                services.AddSingleton<ImageHandlerService>();
                services.AddSingleton<SelectedGameService>();

                services.AddSingleton<LifeCycleService>();
                services.AddSingleton<BusyService>();

                // Core Services
                services.AddSingleton<IFileService, FileService>();
                services.AddSingleton<IGameService, GameService>();
                services.AddSingleton<ISkinManagerService, SkinManagerService>();
                services.AddSingleton<ModCrawlerService>();
                services.AddSingleton<ModSettingsService>();
                services.AddSingleton<ModPresetHandlerService>();
                services.AddSingleton<KeySwapService>();
                services.AddSingleton<ILanguageLocalizer, Localizer>();
                services.AddSingleton<ModPresetService>();
                services.AddSingleton<UserPreferencesService>();
                services.AddSingleton<ArchiveService>();
                services.AddSingleton<ModArchiveRepository>();
                services.AddSingleton<GameBananaCoreService>();
                services.AddSingleton<CommandService>();

                services.AddSingleton<GameBananaService>();

                // Even though I've followed the docs, I keep getting "Exception thrown: 'System.IO.IOException' in System.Net.Sockets.dll"
                // I've read just about every microsoft docs page httpclients, and I can't figure out what I'm doing wrong
                // Also tried with httpclientfactory, but that didn't work either

                services.AddHttpClient<IApiGameBananaClient, ApiGameBananaClient>(client =>
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "JASM-Just_Another_Skin_Manager-Update-Checker");
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                    })
                    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler()
                    {
                        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
                    });

                // I'm preeeetty sure this is not correctly set up, not used to polly 8.x.x
                // But it does rate limit, so I guess it's fine for now
                services.AddResiliencePipeline(ApiGameBananaClient.HttpClientName, (builder, context) =>
                {
                    var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions()
                    {
                        QueueProcessingOrder = QueueProcessingOrder.NewestFirst,
                        QueueLimit = 20,
                        TokenLimit = 5,
                        AutoReplenishment = true,
#if DEBUG
                        TokensPerPeriod = 1,
#else
                        TokensPerPeriod = 5,
#endif
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1)
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
                services.AddSingleton<ModInstallerService>();

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
                services.AddTransient<EasterEggVM>();
                services.AddTransient<EasterEggPage>();
                services.AddTransient<ModsOverviewVM>();
                services.AddTransient<ModsOverviewPage>();
                services.AddTransient<ModInstallerVM>();
                services.AddTransient<ModInstallerPage>();
                services.AddTransient<PresetViewModel>();
                services.AddTransient<PresetPage>();
                services.AddTransient<PresetDetailsViewModel>();
                services.AddTransient<PresetDetailsPage>();
                services.AddTransient<ModSelectorViewModel>();
                services.AddTransient<ModSelector>();
                services.AddTransient<CommandsSettingsViewModel>();
                services.AddTransient<CommandsSettingsPage>();
                services.AddTransient<CharacterGalleryPage>();
                services.AddTransient<CharacterGalleryViewModel>();
                services.AddTransient<CommandProcessViewer>();
                services.AddTransient<CommandProcessViewerViewModel>();
                services.AddTransient<CreateCommandView>();
                services.AddTransient<CreateCommandViewModel>();

                // Configuration
                services.Configure<LocalSettingsOptions>(
                    context.Configuration.GetSection(nameof(LocalSettingsOptions)));
            }).Build();

        UnhandledException += App_UnhandledException;
    }

    // Incremented when an error window is opened, decremented when it is closed
    // Just avoid spamming the user with error windows
    private int _ErrorWindowsOpen = 0;

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Log.Error(e.Exception, """

                               --------------------------------------------------------------------
                                    _   _    ____  __  __                                        
                                   | | / \  / ___||  \/  |                                       
                                _  | |/ _ \ \___ \| |\/| |                                       
                               | |_| / ___ \ ___) | |  | |                                       
                                \___/_/   \_\____/|_|  |_| _ _   _ _____ _____ ____  _____ ____  
                               | ____| \ | |/ ___/ _ \| | | | \ | |_   _| ____|  _ \| ____|  _ \ 
                               |  _| |  \| | |  | | | | | | |  \| | | | |  _| | |_) |  _| | | | |
                               | |___| |\  | |__| |_| | |_| | |\  | | | | |___|  _ <| |___| |_| |
                               |_____|_|_\_|\____\___/_\___/|_|_\_| |_| |_____|_| \_\_____|____/ 
                                  / \  | \ | | | | | | \ | | |/ / \ | |/ _ \ \      / / \ | |    
                                 / _ \ |  \| | | | | |  \| | ' /|  \| | | | \ \ /\ / /|  \| |    
                                / ___ \| |\  | | |_| | |\  | . \| |\  | |_| |\ V  V / | |\  |    
                               /_/___\_\_| \_|__\___/|_|_\_|_|\_\_| \_|\___/  \_/\_/  |_| \_|    
                               | ____|  _ \|  _ \ / _ \|  _ \                                    
                               |  _| | |_) | |_) | | | | |_) |                                   
                               | |___|  _ <|  _ <| |_| |  _ <                                    
                               |_____|_| \_\_| \_\\___/|_| \_\                                   
                               --------------------------------------------------------------------
                               """);

        // show error dialog
        var window = new ErrorWindow(e.Exception, () => _ErrorWindowsOpen--)
        {
            IsAlwaysOnTop = true,
            Title = "JASM - Unhandled Exception",
            SystemBackdrop = new MicaBackdrop()
        };

        window.Activate();
        _ErrorWindowsOpen++;
        window.CenterOnScreen();

        GetService<NotificationManager>()
            .ShowNotification("An error occured!",
                "JASM may be in an unstable state could crash at any moment. It is suggested to restart the app.",
                TimeSpan.FromMinutes(60));

        if (_ErrorWindowsOpen > 4)
        {
            // If there are too many error windows open, just close the app
            // This is to prevent the app from spamming the user with error windows
            Environment.Exit(1);
        }
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