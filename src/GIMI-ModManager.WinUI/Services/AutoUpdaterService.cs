using System.Diagnostics;
using ErrorOr;
using Microsoft.UI.Xaml;
using Serilog;

namespace GIMI_ModManager.WinUI.Services;

public class AutoUpdaterService
{
    private readonly ILogger _logger;
    private readonly UpdateChecker _updateChecker;

    public const string AutoUpdaterFolder = "JASM - Auto Updater";
    public const string NewAutoUpdaterFolder = "JASM - Auto Updater_New";
    public const string AutoUpdaterExe = "JASM - Auto Updater.exe";

    private readonly DirectoryInfo _oldAutoUpdaterFolder = new(Path.Combine(App.ROOT_DIR, AutoUpdaterFolder));

    private readonly DirectoryInfo _newAutoUpdaterFolder = new(Path.Combine(App.ROOT_DIR, NewAutoUpdaterFolder));

    private readonly DirectoryInfo _currentAutoUpdaterFolder = new(Path.Combine(App.ROOT_DIR, AutoUpdaterFolder));

    // Not really a proper lock, but it's good enough for this purposes.
    // Upper [RelayCommand]'s also stop the user from starting multiple update processes.
    private static bool HasStartedSelfUpdateProcess { get; set; }

    public bool AutoUpdaterExists =>
        _currentAutoUpdaterFolder.Exists && ContainsAutoUpdaterExe(_currentAutoUpdaterFolder);

    public AutoUpdaterService(ILogger logger, UpdateChecker updateChecker)
    {
        _logger = logger;
        _updateChecker = updateChecker;
    }


    public void UpdateAutoUpdater()
    {
        var isValidNewAutoUpdater = _newAutoUpdaterFolder.Exists &&
                                    ContainsAutoUpdaterExe(_newAutoUpdaterFolder);
        // There is no new auto updater, so we can't update the current one.
        if (!isValidNewAutoUpdater)
        {
            var isValidOldAutoUpdater = _oldAutoUpdaterFolder.Exists &&
                                        ContainsAutoUpdaterExe(_oldAutoUpdaterFolder);

            if (!isValidOldAutoUpdater)
            {
                _logger.Information("No auto updater found, auto updating is disabled.");
                return;
            }


            _logger.Debug("No new auto updater found, using current one.");
            return;
        }

        // There is a new auto updater, so we can update the current one.
        try
        {
            App.OverrideShutdown = true;
            if (_oldAutoUpdaterFolder.Exists)
            {
                _logger.Information("Updating Auto Updater...");
                _oldAutoUpdaterFolder.Delete(true);
            }
            else
                _logger.Debug("No old auto updater found, using new one.");


            _newAutoUpdaterFolder.MoveTo(_oldAutoUpdaterFolder.FullName);
            _logger.Information("Auto Updater updated successfully.");
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to update Auto Updater.");
        }
        finally
        {
            App.OverrideShutdown = false;
        }
    }


    public Error[]? StartSelfUpdateProcess()
    {
        if (HasStartedSelfUpdateProcess)
        {
            _logger.Warning("Self update process already started.");
            return new[] { Error.Conflict(description:"Self update process already started.") };
        }

        HasStartedSelfUpdateProcess = true;
        _logger.Information("Starting self update process...");
        try
        {
            var errors = _startSelfUpdateProcess();
            return errors;
        }
        finally
        {
            HasStartedSelfUpdateProcess = false;
        }
    }

    private Error[]? _startSelfUpdateProcess()
    {
        if (!_currentAutoUpdaterFolder.Exists)
        {
            _logger.Error("Current auto updater folder does not exist. Could not find the update folder: {Folder}",
                _currentAutoUpdaterFolder.FullName);
            return new[] { Error.NotFound(description: $"Current auto updater folder does not exist. Could not find the update folder: {_currentAutoUpdaterFolder.FullName}") };
        }

        if (!ContainsAutoUpdaterExe(_currentAutoUpdaterFolder))
        {
            _logger.Error(
                "Current auto updater folder does not contain the auto updater exe. Could not find {Exe} in {Folder}",
                AutoUpdaterExe, _currentAutoUpdaterFolder.FullName);

            return new[] { Error.NotFound(description: $"Current auto updater folder does not contain the auto updater exe. Could not find {AutoUpdaterExe} in {_currentAutoUpdaterFolder.FullName}") };
        }

        var isAutoUpdaterRunning = Process.GetProcessesByName(AutoUpdaterExe).Any();

        if (isAutoUpdaterRunning)
        {
            _logger.Error("Auto updater is already running.");
            return new[] { Error.Conflict(description: "Auto updater is already running.") };
        }

        try
        {
            _logger.Information("Starting Auto Updater...");
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(_currentAutoUpdaterFolder.FullName, AutoUpdaterExe),
                WorkingDirectory = _currentAutoUpdaterFolder.FullName,
                Arguments = _updateChecker.CurrentVersion.ToString().Trim('v') ?? "",
                UseShellExecute = true
            });

            if (process is null || process.HasExited)
            {
                _logger.Error("Failed to start Auto Updater.");
                return new[] { Error.Unexpected(description:"Failed to start Auto Updater.") };
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to start Auto Updater.");
            return new[]
            {
                Error.Unexpected(description: $"An error occurred while starting the Auto Updater. Reason: {e.Message}")
            };
        }

        _logger.Information("Self update process started successfully. Exiting...");

        Application.Current.Exit();
        return null;
    }

    private static bool ContainsAutoUpdaterExe(DirectoryInfo directoryInfo)
    {
        return directoryInfo.EnumerateFileSystemInfos().Any(f =>
            f.Name.Equals(AutoUpdaterExe, StringComparison.CurrentCultureIgnoreCase));
    }
}