using System.Diagnostics;
using System.Security.Principal;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Options;
using Microsoft.UI.Xaml;
using Serilog;
using Windows.Storage.Pickers;

namespace GIMI_ModManager.WinUI.Services;

public abstract partial class BaseProcessManager : ObservableObject
{
    private protected readonly ILogger _logger;
    private protected readonly ILocalSettingsService _localSettingsService;

    private  Process? _process;
    private protected string _prcoessPath = null!;
    private protected string _workingDirectory = string.Empty;

    private bool _exitHandlerRegistered;
    private protected bool _isGenshinClass;

    public string ProcessName { get; protected set; } = string.Empty;


    [ObservableProperty] private string? _errorMessage = null;

    [ObservableProperty] private ProcessStatus _processStatus = ProcessStatus.NotInitialized;


    protected BaseProcessManager(ILogger logger, ILocalSettingsService localSettingsService)
    {
        _logger = logger;
        _localSettingsService = localSettingsService;
    }

    public abstract Task<bool> TryInitialize();

    private protected bool _tryInitialize(string? processPath)
    {
        if (!File.Exists(processPath))
        {
            return false;
        }

        _prcoessPath = processPath;
        ProcessStatus = ProcessStatus.NotRunning;
        return true;
    }

    public abstract Task SetPath(string processName, string path, string? workingDirectory = null);

    public void StartProcess()
    {
        if (_process is { HasExited: false })
        {
            _logger.Information($"{ProcessName} is already running");
            return;
        }

        if (ProcessStatus == ProcessStatus.NotInitialized)
        {
            _logger.Warning($"{ProcessName} is not initialized");
            return;
        }


        _process = Process.Start(new ProcessStartInfo(_prcoessPath)
        {
            WorkingDirectory = _workingDirectory == string.Empty ? Path.GetDirectoryName(_prcoessPath) ?? "" : _workingDirectory,
            Arguments = _isGenshinClass ? "runas" : "",
            UseShellExecute = _isGenshinClass
            
        });

        if (_process == null || _process.HasExited)
        {
            ProcessStatus = ProcessStatus.NotInitialized;
            ErrorMessage = $"Failed to start {ProcessName}";
            _logger.Error($"Failed to start {ProcessName}");
            return;
        }

        ProcessStatus = ProcessStatus.Running;

        _process.Exited += (sender, args) =>
        {
            ProcessStatus = ProcessStatus.NotRunning;
            _logger.Information("{ProcessName} exited with exit code: {ExitCode}", ProcessName, _process.ExitCode);
        };

        if (_exitHandlerRegistered) return;


        App.MainWindow.Closed += MainWindowExitHandler;
        _exitHandlerRegistered = true;
    }

    private void MainWindowExitHandler(object sender, WindowEventArgs args)
        => StopProcess();


    public void CheckStatus()
    {

        if (ProcessStatus == ProcessStatus.NotInitialized) return;
        ProcessStatus = _process is { HasExited: false }  ? ProcessStatus.Running : ProcessStatus.NotRunning;
    }

    public void StopProcess()
    {
        if (_process is { HasExited: false })
        {
            _logger.Information($"Killing {ProcessName}");
            _process.Kill();
            _logger.Debug($"{ProcessName} killed");
        }
    }

    /// <summary>
    /// Set path with SetPath()
    /// </summary>
    /// <param name="windowHandle"></param>
    /// <returns></returns>
    public async Task<string?> PickProcessPathAsync(Window windowHandle)
    {
        var exeFilePicker = new FileOpenPicker();
        exeFilePicker.FileTypeFilter.Add(".exe");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(windowHandle);
        WinRT.Interop.InitializeWithWindow.Initialize(exeFilePicker, hwnd);
        var chosenExePath = await exeFilePicker.PickSingleFileAsync();

        if (chosenExePath is null)
        {
            _logger.Information("User cancelled 3Dmigoto exe selection");
            return null;
        }

        if (chosenExePath.FileType != ".exe")
        {
            _logger.Warning("User selected a file that is not an exe");
            return null;
        }

        if (!File.Exists(chosenExePath.Path))
        {
            _logger.Warning("User selected a file that does not exist");
            return null;
        }

        return chosenExePath.Path;
    }
}

public enum ProcessStatus
{
    NotInitialized,
    NotRunning,
    Running,
}

public class GenshinProcessManager : BaseProcessManager
{
    public GenshinProcessManager(ILogger logger, ILocalSettingsService localSettingsService) : base(logger,
        localSettingsService)
    {
    }

    public override async Task<bool> TryInitialize()
    {
        var processOptions = await _localSettingsService.ReadSettingAsync<ProcessOptions>(ProcessOptions.Key) ??
                             new ProcessOptions();
        _isGenshinClass = true;
        return _tryInitialize(processOptions.GenshinExePath);
    }

    public override async Task SetPath(string processName, string path, string? workingDirectory = null)
    {
        ProcessName = processName;
        _prcoessPath = path;
        _workingDirectory = workingDirectory ?? Path.GetDirectoryName(path) ?? "";
        var processOptions = await _localSettingsService.ReadSettingAsync<ProcessOptions>(ProcessOptions.Key) ??
                             new ProcessOptions();
        processOptions.GenshinExePath = path;

        await _localSettingsService.SaveSettingAsync(ProcessOptions.Key, processOptions);
        _logger.Information("Saved Genshin path to settings");
        ProcessStatus = ProcessStatus.NotRunning;
    }
}

public class ThreeDMigtoProcessManager : BaseProcessManager
{
    public ThreeDMigtoProcessManager(ILogger logger, ILocalSettingsService localSettingsService) : base(logger,
        localSettingsService)
    {
    }

    public override async Task<bool> TryInitialize()
    {
        var processOptions = await _localSettingsService.ReadSettingAsync<ProcessOptions>(ProcessOptions.Key) ??
                             new ProcessOptions();
        return _tryInitialize(processOptions.MigotoExePath);
    }

    public override async Task SetPath(string processName, string path, string? workingDirectory = null)
    {
        ProcessName = processName;
        _prcoessPath = path;
        _workingDirectory = workingDirectory ?? Path.GetDirectoryName(path) ?? "";
        var processOptions = await _localSettingsService.ReadSettingAsync<ProcessOptions>(ProcessOptions.Key) ??
                             new ProcessOptions();
        processOptions.MigotoExePath = path;

        await _localSettingsService.SaveSettingAsync(ProcessOptions.Key, processOptions);
        _logger.Information("Saved 3Dmigoto path to settings");
        ProcessStatus = ProcessStatus.NotRunning;
    }
}