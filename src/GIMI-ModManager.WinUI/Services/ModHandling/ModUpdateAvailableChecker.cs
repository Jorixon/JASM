using System.Diagnostics;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using Newtonsoft.Json;
using Serilog;
using static GIMI_ModManager.WinUI.Services.ModHandling.ModUpdateAvailableChecker;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public sealed class ModUpdateAvailableChecker
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly GameBananaService _gameBananaService;
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;
    private readonly ModNotificationManager _modNotificationManager;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly Pauser _pauser = new();

    private CancellationTokenSource? _stoppingCancellationTokenSource;

    private CancellationTokenSource? _waiterCancellationTokenSource;
    private CancellationTokenSource? _runningCancellationTokenSource;

    private CancellationToken _waiterCancellationToken = default;
    private CancellationToken _runningCancellationToken = default;
    private bool _isRunning;
    private bool _forceUpdate;

    private List<Guid>? _checkOnlyMods;
    private readonly TimeSpan _waitTime = TimeSpan.FromMinutes(30);

    public event EventHandler<UpdateCheckerEvent>? OnUpdateCheckerEvent;
    public RunningState Status { get; private set; }
    public DateTime? NextRunAt { get; private set; }


    public ModUpdateAvailableChecker(ISkinManagerService skinManagerService, ILogger logger,
        ModNotificationManager modNotificationManager, GameBananaService gameBananaService,
        ILocalSettingsService localSettingsService, NotificationManager notificationManager)
    {
        _skinManagerService = skinManagerService;
        _modNotificationManager = modNotificationManager;
        _gameBananaService = gameBananaService;
        _localSettingsService = localSettingsService;
        _notificationManager = notificationManager;
        _logger = logger.ForContext<ModUpdateAvailableChecker>();
    }

    public async Task InitializeAsync()
    {
        _stoppingCancellationTokenSource = new CancellationTokenSource();

        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
            .ConfigureAwait(false);

        if (!settings.Enabled)
            _pauser.Pause();


        var stoppingToken = _stoppingCancellationTokenSource.Token;

        Task.Run(() => StartBackgroundChecker(stoppingToken), CancellationToken.None);
    }

    private async Task StartBackgroundChecker(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Status = RunningState.Stopped;
            OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status));
            await _pauser.EnterAsync().ConfigureAwait(false);


            _waiterCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _runningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            if (!_skinManagerService.IsInitialized)
                SpinWait.SpinUntil(() => _skinManagerService.IsInitialized || stoppingToken.IsCancellationRequested);

            Status = RunningState.Running;
            OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status));
            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                await RunCheckerAsync().ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _logger.Error(e,
                    "An error occurred while checking for mod updates. Stopping background mod update checker...");
                _notificationManager.ShowNotification(
                    "An error occurred while checking for mod updates.",
                    "Stopping background mod update checker...",
                    TimeSpan.FromSeconds(20));
                Status = RunningState.Error;
                break;
            }
            finally
            {
                _waiterCancellationTokenSource?.Dispose();
                _runningCancellationTokenSource?.Dispose();
            }

            Status = RunningState.Stopped;
        }

        _logger.Debug("ModUpdateAvailableChecker stopped");
    }

    private async Task RunCheckerAsync()
    {
        try
        {
            _isRunning = true;
            _runningCancellationToken = _runningCancellationTokenSource!.Token;
            _logger.Information("Checking for mod updates...");

            Status = RunningState.Running;
            OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status));

            var stopWatch = Stopwatch.StartNew();
            await CheckForUpdates(_forceUpdate, cancellationToken: _runningCancellationToken).ConfigureAwait(false);
            stopWatch.Stop();

            _logger.Debug("Finished checking for mod updates in {Elapsed}", stopWatch.Elapsed);
            _notificationManager.ShowNotification("Finished checking for mod updates",
                _checkOnlyMods is not null
                    ? $"Checked {_checkOnlyMods.Count} mods for updates"
                    : $"Finished checking for mod updates", TimeSpan.FromSeconds(4));
        }

        finally
        {
            _forceUpdate = false;
            _isRunning = false;
            _checkOnlyMods = null;
        }


        _waiterCancellationToken = _waiterCancellationTokenSource!.Token;
        await WaitForNextCheck(_waiterCancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForNextCheck(CancellationToken token)
    {
        try
        {
            _logger.Information("Next update check will run at {WaitTime}", DateTime.Now.Add(_waitTime));
            Status = RunningState.Waiting;
            NextRunAt = DateTime.Now + _waitTime;
            OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status, NextRunAt));
            await Task.Delay(_waitTime, token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }

        NextRunAt = null;
    }


    private async Task CheckForUpdates(bool forceUpdate = false, CancellationToken cancellationToken = default)
    {
        var characterModLists = _skinManagerService.CharacterModLists;
        var modEntries = characterModLists.SelectMany(x => x.Mods);

        if (_checkOnlyMods is not null)
            modEntries = modEntries.Where(x => _checkOnlyMods.Contains(x.Mod.Id));


        var tasks = new List<Task<ModsRetrievedResult>>();
        var taskToMod = new Dictionary<Task<ModsRetrievedResult>, CharacterSkinEntry>();
        foreach (var characterSkinEntry in modEntries)
        {
            var mod = characterSkinEntry.Mod;
            cancellationToken.ThrowIfCancellationRequested();
            var modSettings = await mod.Settings.ReadSettingsAsync().ConfigureAwait(false);

            if (modSettings.ModUrl is null)
                continue;

            if (modSettings.LastChecked is not null && (modSettings.LastChecked >
                    DateTime.Now.Subtract(TimeSpan.FromMinutes(15)) && !forceUpdate))
            {
                _logger.Debug("Skipping update check for {modName}, last update check was {lastUpdate}", mod.Name,
                    modSettings.LastChecked);
                continue;
            }

            var fetchTask = Task.Run(() => _gameBananaService.GetAvailableMods(mod.Id, cancellationToken),
                cancellationToken);

            tasks.Add(fetchTask);
            taskToMod.Add(fetchTask, characterSkinEntry);
        }

        if (!tasks.Any())
        {
            _logger.Debug("No mods to check for updates");
            return;
        }


        while (tasks.Any())
        {
            var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
            tasks.Remove(finishedTask);
            var characterSkinEntry = taskToMod[finishedTask];
            try
            {
                var result = await finishedTask;
                await UpdateLastChecked(characterSkinEntry.Mod, result).ConfigureAwait(false);
                if (result.AnyNewMods)
                {
                    await AddModNotifications(characterSkinEntry, result).ConfigureAwait(false);
                    _logger.Information("New or updated mods are available for {ModName}", characterSkinEntry.Mod.Name);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error occurred while checking for mod update for {ModName}",
                    characterSkinEntry.Mod.Name);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }


    private Task UpdateLastChecked(ISkinMod mod, ModsRetrievedResult result)
    {
        var modSettingsResult = mod.Settings.GetSettings();

        if (!modSettingsResult.IsT0)
            throw new InvalidOperationException("Failed to get mod settings");

        var modSettings = modSettingsResult.AsT0;

        return mod.Settings.SaveSettingsAsync(modSettings.DeepCopyWithProperties(newLastChecked: DateTime.Now));
    }

    private Task AddModNotifications(CharacterSkinEntry characterSkinEntry, ModsRetrievedResult result)
    {
        var modSettingsResult = characterSkinEntry.Mod.Settings.GetSettings();

        if (!modSettingsResult.IsT0)
            throw new InvalidOperationException("Failed to get mod settings");

        var modSettings = modSettingsResult.AsT0;

        var modNotification = new ModNotification()
        {
            AttentionType = AttentionType.UpdateAvailable,
            CharacterInternalName = characterSkinEntry.ModList.Character.InternalName.Id,
            ModId = characterSkinEntry.Mod.Id,
            ModCustomName = modSettings.CustomName ?? characterSkinEntry.Mod.Name,
            ModFolderName = characterSkinEntry.Mod.Name,
            Message = $"New or updated mods are available for {characterSkinEntry.Mod.GetNameWithoutDisabledPrefix()}",
            ModsRetrievedResult = result
        };

        return _modNotificationManager.AddModNotification(modNotification, persistent: true);
    }

    private bool _isStartingUpdate;

    public void CheckNow(IEnumerable<Guid>? checkOnlyMods = null, bool forceUpdate = false,
        CancellationToken cancellationToken = default)
    {
        if (_isStartingUpdate)
            return;
        _isStartingUpdate = true;


        if (_pauser.IsPaused)
        {
            _pauser.RunOnce();

            if (!_waiterCancellationToken.IsCancellationRequested && !_runningCancellationToken.IsCancellationRequested)
            {
                _waiterCancellationTokenSource?.Cancel();
                _runningCancellationTokenSource?.Cancel();
            }
        }
        else
        {
            _waiterCancellationTokenSource?.Cancel();
            _runningCancellationTokenSource?.Cancel();
        }

        SpinWait.SpinUntil(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return !_isRunning;
        });

        _forceUpdate = forceUpdate;
        _checkOnlyMods = checkOnlyMods?.ToList();
        _isStartingUpdate = false;
    }

    public void CancelAndStop()
    {
        _stoppingCancellationTokenSource?.Cancel();
    }

    public async Task StopAsync()
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
            .ConfigureAwait(false);
        settings.Enabled = false;
        await _localSettingsService.SaveSettingAsync(BackGroundModCheckerSettings.Key, settings).ConfigureAwait(false);
        if (Status == RunningState.Error)
            return;

        _pauser.Pause();
        _waiterCancellationTokenSource?.Cancel();
        _runningCancellationTokenSource?.Cancel();
        _logger.Information("Disabled background mod update checker");
    }

    public async Task StartBackgroundCheckerAsync()
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
            .ConfigureAwait(false);
        settings.Enabled = true;
        await _localSettingsService.SaveSettingAsync(BackGroundModCheckerSettings.Key, settings).ConfigureAwait(false);
        if (Status == RunningState.Error)
            return;
        _pauser.Resume();
        _logger.Information("Enabled background mod update checker");
    }

    public enum RunningState
    {
        Running,
        Waiting,
        Stopped,
        Error
    }

    public bool IsReady => Status is RunningState.Stopped or RunningState.Waiting;
}

public class BackGroundModCheckerSettings
{
    [JsonIgnore] public const string Key = "BackGroundModCheckerSettings";
    public bool Enabled { get; set; } = true;
}

public class UpdateCheckerEvent : EventArgs
{
    public RunningState State { get; }
    public DateTime? NextRunAt { get; }

    public UpdateCheckerEvent(RunningState state, DateTime? nextRunAt = null)
    {
        State = state;
        NextRunAt = nextRunAt;
    }
}

public sealed class Pauser : IDisposable
{
    private readonly object _lock = new();
    public bool IsPaused { get; private set; }
    public bool IgnoreOnce { get; set; }

    private CancellationTokenSource? _cancellationTokenSource;

    private Task _pause()
    {
        return Task.Delay(-1, _cancellationTokenSource?.Token ?? CancellationToken.None);
    }

    public async Task EnterAsync()
    {
        lock (_lock)
        {
            if (IgnoreOnce)
            {
                IgnoreOnce = false;
                return;
            }

            if (!IsPaused)
                return;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        try
        {
            await _pause().ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }
    }

    public void RunOnce()
    {
        lock (_lock)
        {
            _cancellationTokenSource?.Cancel();
            IgnoreOnce = true;
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            IsPaused = false;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            IsPaused = true;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}