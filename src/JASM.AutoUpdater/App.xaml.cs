using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Serilog;
using WinUIEx;
using UnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JASM.AutoUpdater;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();


        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .WriteTo.File("logs\\auto-updater-log.txt")
            .WriteTo.Console()
            .CreateLogger();

        Log.Logger = logger;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Information("AutoUpdater OnLaunched");

        MainWindow = new MainWindow();
        MainWindow.Activate();
        MainWindow.SetWindowSize(900, 600);
        MainWindow.IsMaximizable = false;
        MainWindow.IsMinimizable = false;
        MainWindow.IsResizable = false;
        MainWindow.Title = "JASM Auto Updater";

        var arguments = Environment.GetCommandLineArgs();

        MainWindow.Content = new MainPage(arguments.Skip(1).FirstOrDefault() ?? string.Empty);
        MainWindow.BringToFront();

        UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception");
    }

    internal static MainWindow MainWindow;
}