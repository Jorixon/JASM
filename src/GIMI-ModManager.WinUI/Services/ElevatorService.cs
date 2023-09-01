using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.UI.Xaml;
using System.Security.Principal;
using Windows.System;

namespace GIMI_ModManager.WinUI.Services;

public partial class ElevatorService : ObservableRecipient
{
    public const string ElevatorPipeName = "MyPipess";
    public const string ElevatorProcessName = "Elevator.exe";
    private readonly ILogger _logger;

    [ObservableProperty] private ElevatorStatus _elevatorStatus = ElevatorStatus.NotRunning;
    private Process? _elevatorProcess;

    public string? ErrorMessage { get; private set; }

    private bool _exitHandlerRegistered;

    private bool _IsInitialized;

    public ElevatorService(ILogger logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        if (_IsInitialized) throw new InvalidOperationException("ElevatorService is already initialized");
        _logger.Information("Initializing ElevatorService");
        var elevatorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ElevatorProcessName);
        if (Path.Exists(elevatorPath))
        {
            _logger.Debug(ElevatorProcessName + " found at: " + elevatorPath);
            return;
        }

        _logger.Warning("Elevator.exe not found");
        ErrorMessage = "Elevator.exe not found";
        ElevatorStatus = ElevatorStatus.InitializingFailed;
    }

    public void StartElevator()
    {
        if (_elevatorProcess is { HasExited: false })
        {
            _logger.Information("Elevator.exe is already running");
            return;
        }

        var currentUser = WindowsIdentity.GetCurrent().Name;
        currentUser = currentUser.Split("\\").LastOrDefault() ?? currentUser;

        _elevatorProcess = Process.Start(new ProcessStartInfo(ElevatorProcessName)
        {
            UseShellExecute = true,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Hidden,
            Verb = "runas",
            ArgumentList = { currentUser }
        });

        if (_elevatorProcess == null || _elevatorProcess.HasExited)
        {
            ElevatorStatus = ElevatorStatus.InitializingFailed;
            ErrorMessage = "Failed to start Elevator.exe";
            _logger.Error("Failed to start Elevator.exe");
            return;
        }

        ElevatorStatus = ElevatorStatus.Running;

        _elevatorProcess.Exited += (sender, args) =>
        {
            ElevatorStatus = ElevatorStatus.NotRunning;
            _logger.Information("Elevator.exe exited with exit code: {ExitCode}", _elevatorProcess.ExitCode);
        };

        if (_exitHandlerRegistered) return;


        App.MainWindow.Closed += MainWindowExitHandler;
        _exitHandlerRegistered = true;
    }


    private void MainWindowExitHandler(object sender, WindowEventArgs args)
    {
        if (_elevatorProcess is { HasExited: false })
        {
            _logger.Information("Killing Elevator.exe");
            _elevatorProcess.Kill();
            _logger.Debug("Elevator.exe killed");
        }
    }

    public async Task RefreshGenshinMods()
    {
        try
        {
            await using var pipeClient = new NamedPipeClientStream(".", ElevatorPipeName, PipeDirection.Out);
            await pipeClient.ConnectAsync(TimeSpan.FromSeconds(5), default);
            await using var writer = new StreamWriter(pipeClient);
            _logger.Debug("Sending command: {Command}", nameof(RefreshGenshinMods));
            await writer.WriteLineAsync("0");
            await writer.FlushAsync();
            _logger.Debug("Done");
        }
        catch (TimeoutException e)
        {
            _logger.Error(e, "Failed to Refresh Genshin Mods");
        }
    }

    public void CheckStatus()
    {
        ElevatorStatus = _elevatorProcess is { HasExited: false } ? ElevatorStatus.Running : ElevatorStatus.NotRunning;
    }
}

public enum ElevatorStatus
{
    InitializingFailed = -1,
    NotRunning = 0,
    Running
}