using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
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


        var logger = CreateLogger();

        Log.Logger = logger;
    }

    private static ILogger CreateLogger() => new LoggerConfiguration()
        .Enrich.FromLogContext()
        .MinimumLevel.Information()
        .WriteTo.File(LogFilePath)
        .WriteTo.Console()
        .CreateLogger();

    private static readonly string LogFilePath =
        $@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\logs\auto-updater-log.txt";

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
        Log.Information("------");
        Log.CloseAndFlush();

        e.Handled = true;

        var result = PInvoke.MessageBox(
            new HWND(WinRT.Interop.WindowNative.GetWindowHandle(MainWindow)),
            $"Check the logs file for more info: {LogFilePath}\n\n" +
            $"Press Yes to close the program. Press No to ignore the error and continue",
            $"An error occured: {e.Exception.GetType()} | {e.Exception.Message ?? "null"}",
            MESSAGEBOX_STYLE.MB_ICONSTOP | MESSAGEBOX_STYLE.MB_YESNO | MESSAGEBOX_STYLE.MB_APPLMODAL |
            MESSAGEBOX_STYLE.MB_SETFOREGROUND);

        if (result == MESSAGEBOX_RESULT.IDYES)
        {
            Current.Exit();
            return;
        }

        Log.Logger = CreateLogger();

        var mainPage = MainWindow.Content as MainPage;
        MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                mainPage?.ViewModel?.Stop("An unknown error occured..");
            }
            catch (Exception exception)
            {
                return;
            }
        });

        return;
    }

    internal static MainWindow MainWindow = null!;
}