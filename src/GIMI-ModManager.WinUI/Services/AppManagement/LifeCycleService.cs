using System.Diagnostics;
using System.Reflection;
using GIMI_ModManager.Core.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.AppManagement;

public class LifeCycleService
{
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;

    public LifeCycleService(ILogger logger, NotificationManager notificationManager)
    {
        _notificationManager = notificationManager;
        _logger = logger.ForContext<LifeCycleService>();
    }

    /// <summary>
    /// This method will try to restart the app using the suggested method from Microsoft.
    /// </summary>
    /// <param name="args"></param>
    /// <param name="useLegacyRestartOnError"></param>
    /// <param name="notifyOnError"></param>
    /// <returns></returns>
    public async Task RestartAsync(string args = "", bool useLegacyRestartOnError = true, bool notifyOnError = false)
    {
        var error = AppInstance.Restart(args);


        if (notifyOnError)
        {
            _notificationManager.ShowNotification("Error restarting app",
                useLegacyRestartOnError
                    ? $"Trying legacy restart method. Reason: {error}"
                    : $"Please restart manually. Reason: {error}",
                TimeSpan.FromSeconds(4));
        }


        if (!useLegacyRestartOnError)
        {
            _logger.Error("Error restarting app: {Error}", error);
            return;
        }

        _logger.Warning("Error restarting app: {Error}. Falling back to legacy restart method", error);
        await Task.Delay(1);
        await LegacyRestartAsync(args).ConfigureAwait(false);
    }

    /// <summary>
    /// This method is used as a fallback if the new restart method fails.
    /// It will try to restart the app by starting a new process of itself and then exit the current process.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public async Task LegacyRestartAsync(string args = "")
    {
        var exePath = Assembly.GetEntryAssembly()!.Location;
        exePath = Path.ChangeExtension(exePath, ".exe");

        if (exePath.IsNullOrEmpty() || !File.Exists(exePath))
        {
            exePath = Environment.ProcessPath;
            exePath = Path.ChangeExtension(exePath, ".exe");
            _logger.Debug("Restarting from process path: {ExePath}", exePath);
        }

        if (exePath.IsNullOrEmpty() || !File.Exists(exePath))
        {
            _logger.Error("Unable to find exe path at {ExePath}. Shutting down...", exePath);
            Application.Current.Exit();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Arguments = args
            });
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error restarting app");
            _notificationManager.ShowNotification("Error restarting app", "Please restart manually",
                TimeSpan.FromSeconds(4));
            await Task.Delay(TimeSpan.FromSeconds(3));
            Application.Current.Exit();
            return;
        }


        Application.Current.Exit();
    }
}