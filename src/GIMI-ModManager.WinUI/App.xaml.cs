using System.Diagnostics;
using System.Globalization;
using Windows.Storage;
using GIMI_ModManager.Core.Contracts.Services;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using Serilog.Templates;
using WinUI3Localizer;

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
    public static WindowEx MainWindow { get; } = new MainWindow();

    public static ILocalizer Localizer { get; private set; } = null!;

    public static UIElement? AppTitlebar { get; set; }

    public static bool OverrideShutdown { get; set; }

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
                configuration.Enrich.FromLogContext();
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

                // Core Services
                services.AddSingleton<IFileService, FileService>();
                services.AddSingleton<IGenshinService, GenshinService>();
                services.AddSingleton<ISkinManagerService, SkinManagerService>();
                services.AddSingleton<ModCrawlerService>();
                services.AddSingleton<ModSettingsService>();
                services.AddSingleton<KeySwapService>();

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
    }


    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await InitializeLocalizer();
        NotImplemented.NotificationManager = GetService<NotificationManager>();
        base.OnLaunched(args);
        await GetService<IActivationService>().ActivateAsync(args);
    }

    private async Task InitializeLocalizer()
    {
        // Initialize a "Strings" folder in the executables folder.
        var StringsFolderPath = Path.Combine(AppContext.BaseDirectory, "Strings");
        var stringsFolder = await StorageFolder.GetFolderFromPathAsync(StringsFolderPath);

        Localizer = await new LocalizerBuilder()
            .AddStringResourcesFolderForLanguageDictionaries(StringsFolderPath)
            .SetOptions(options => { options.DefaultLanguage = "en-us"; })
            .Build();

        var ci = CultureInfo.CurrentUICulture.Name.ToLower();

        Log.Information("Current culture: {ci}", ci);

        if (ci != "en-us")
            if (Localizer.GetAvailableLanguages().Contains(ci))
                await Localizer.SetLanguage(ci);
    }
}