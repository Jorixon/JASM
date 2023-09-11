using System.Diagnostics;
using Windows.ApplicationModel;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Activation;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using Microsoft.UI.Xaml.Controls;
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
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static string TMP_DIR { get; } = Path.Combine(Path.GetTempPath(), "JASM_TMP");
    public static WindowEx MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar { get; set; }

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

                services.AddSingleton<ElevatorService>();
                services.AddSingleton<GenshinProcessManager>();
                services.AddSingleton<ThreeDMigtoProcessManager>();

                services.AddSingleton<UpdateChecker>();

                // Core Services
                services.AddSingleton<IFileService, FileService>();
                services.AddSingleton<IGenshinService, GenshinService>();
                services.AddSingleton<ISkinManagerService, SkinManagerService>();

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
        NotImplemented.NotificationManager = App.GetService<NotificationManager>();
        base.OnLaunched(args);
        await App.GetService<IActivationService>().ActivateAsync(args);
    }
}