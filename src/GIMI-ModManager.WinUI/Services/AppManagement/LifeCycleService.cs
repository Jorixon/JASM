using System.Diagnostics;
using System.Reflection;
using CommunityToolkitWrapper;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Services.AppManagement.Updating;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.AppManagement;

public class LifeCycleService(
    ILogger logger,
    NotificationManager notificationManager,
    ILocalSettingsService localSettingsService,
    ModNotificationManager modNotificationManager,
    IWindowManagerService windowManagerService,
    UpdateChecker updateChecker,
    ModUpdateAvailableChecker modUpdateAvailableChecker,
    CommandService commandService)
{
    private readonly ILogger _logger = logger.ForContext<LifeCycleService>();
    private readonly NotificationManager _notificationManager = notificationManager;
    private readonly ILocalSettingsService _localSettingsService = localSettingsService;
    private ModNotificationManager _modNotificationManager = modNotificationManager;
    private readonly IWindowManagerService _windowManagerService = windowManagerService;
    private readonly UpdateChecker _updateChecker = updateChecker;
    private readonly ModUpdateAvailableChecker _modUpdateAvailableChecker = modUpdateAvailableChecker;
    private readonly CommandService _commandService = commandService;

    /// <summary>
    /// This method will try to restart the app using the suggested method from Microsoft.
    /// </summary>
    /// <param name="args">Args to pass to the app when starting up again</param>
    /// <param name="useLegacyRestartOnError">Fallback to manually starting the app right before closing</param>
    /// <param name="notifyOnError"></param>
    /// <param name="postShutdownLogic">Shutdown logic to run after the main application shutdown logic right before restarting. Some services may have been stopped at this point</param>
    /// <returns></returns>
    public async Task RestartAsync(string args = "", bool useLegacyRestartOnError = true, bool notifyOnError = false,
        Func<Task>? postShutdownLogic = null)
    {
        await StartShutdownAsync(false);
        if (postShutdownLogic is not null)
            await postShutdownLogic();
        _logger.Debug("Restarting app with args: {Args}", args);
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
            await StartShutdownAsync();
            return;
        }


        await StartShutdownAsync().ConfigureAwait(false);
    }

    public Task<nint?> CheckIfAlreadyRunningAsync()
    {
        var otherProcess = GetOtherInstanceProcess();

        return otherProcess == null
            ? Task.FromResult<IntPtr?>(null)
            : Task.FromResult<IntPtr?>(otherProcess.MainWindowHandle);
    }

    public Process? GetOtherInstanceProcess()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);

            if (processes.Length <= 1) return null;

            var currentProcessId = currentProcess.Id;
            var currentProcessName = currentProcess.ProcessName;

            foreach (var process in processes)
            {
                if (process.Id == currentProcessId) continue;

                var processName = process.ProcessName;


                if (currentProcessName!.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    return process;
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error checking for other instances of the app");
            return null;
        }


        return null;
    }

    public async Task StartShutdownAsync(bool shutdown = true)
    {
        App.IsShuttingDown = true;
        try
        {
            await ShutdownCleanupAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error during shutdown cleanup");
        }
        finally
        {
            App.ShutdownComplete = true;
            if (shutdown)
            {
                _logger.Debug("Shutting down application...");

                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    Application.Current.Exit();
                    App.MainWindow.Close();
                });
            }
        }
    }

    private async Task ShutdownCleanupAsync()
    {
        _logger.Debug("JASM starting shutdown cleanup...");

        if (App.OverrideShutdown)
        {
            _logger.Information("Shutdown override enabled, skipping shutdown...");
            _logger.Information("Shutdown override will be disabled in at most 1 second.");
            await Task.Run(async () =>
            {
                await Task.Delay(500);
                App.OverrideShutdown = false;
                _logger.Information("Shutdown override disabled.");
            });
            await StartShutdownAsync();
        }


        var notificationCleanupTask =
            Task.Run(async () => await _modNotificationManager.CleanupAsync().ConfigureAwait(false));

        var stopBackgroundTasks = Task.Run(() =>
        {
            _modUpdateAvailableChecker.CancelAndStop();
            _updateChecker.CancelAndStop();
            _notificationManager.CancelAndStop();
            commandService.Cleanup();
        });

        await notificationCleanupTask;

        if (DispatcherQueue.GetForCurrentThread() is not null)
        {
            await SaveWindowSettingsAsync();
            await _windowManagerService.CloseWindowsAsync().ConfigureAwait(false);
        }
        else
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
            {
                await SaveWindowSettingsAsync();
                await _windowManagerService.CloseWindowsAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


        var tmpDirCleanupTask = Task.Run(() =>
        {
            var tmpDir = new DirectoryInfo(App.TMP_DIR);
            if (tmpDir.Exists)
            {
                tmpDir.Refresh();
                _logger.Debug("Deleting temporary directory: {Path}", tmpDir.FullName);
                try
                {
                    tmpDir.EnumerateFiles("*", SearchOption.AllDirectories)
                        .ForEach(f => f.Attributes = FileAttributes.Normal);
                    tmpDir.EnumerateDirectories("*", SearchOption.AllDirectories)
                        .ForEach(d => d.Attributes = FileAttributes.Normal);
                    tmpDir.Delete(true);
                }
                catch (Exception e)
                {
                    _logger.Warning(e, "Failed to delete temporary directory: {Path}", tmpDir.FullName);
                }
            }
        });

        await stopBackgroundTasks.ConfigureAwait(false);
        await tmpDirCleanupTask.ConfigureAwait(false);


        _logger.Debug("JASM shutdown cleanup complete.");
    }

    private async Task SaveWindowSettingsAsync()
    {
        if (App.MainWindow is null || App.MainWindow.AppWindow is null)
            return;


        var windowSettings = await _localSettingsService
            .ReadOrCreateSettingAsync<ScreenSizeSettings>(ScreenSizeSettings.Key);

        if (windowSettings is { PersistWindowPosition: false, PersistWindowSize: false })
        {
            await _localSettingsService.SaveSettingAsync(ScreenSizeSettings.Key, new ScreenSizeSettings()
            {
                PersistWindowPosition = false,
                PersistWindowSize = false
            })
                .ConfigureAwait(false);
            return;
        }


        var isFullScreen = App.MainWindow.WindowState == WindowState.Maximized;

        var width = windowSettings.Width;
        var height = windowSettings.Height;
        var xPosition = windowSettings.XPosition;
        var yPosition = windowSettings.YPosition;


        if (!isFullScreen && App.MainWindow.WindowState != WindowState.Minimized)
        {
            width = (int)App.MainWindow.Width;
            height = (int)App.MainWindow.Height;
        }

        if (App.MainWindow.WindowState != WindowState.Minimized)
        {
            xPosition = App.MainWindow.AppWindow.Position.X;
            yPosition = App.MainWindow.AppWindow.Position.Y;
        }

        _logger.Debug($"Saving Window size: {width}x{height} | IsFullscreen: {isFullScreen}");

        var newWindowSettings = new ScreenSizeSettings(width, height)
        {
            IsFullScreen = isFullScreen,
            XPosition = xPosition,
            YPosition = yPosition,
            PersistWindowPosition = windowSettings.PersistWindowPosition,
            PersistWindowSize = windowSettings.PersistWindowSize
        };

        await _localSettingsService.SaveSettingAsync(ScreenSizeSettings.Key, newWindowSettings)
            .ConfigureAwait(false);
    }
}