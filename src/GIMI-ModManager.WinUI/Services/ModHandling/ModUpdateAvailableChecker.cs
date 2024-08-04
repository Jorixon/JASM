using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Services.GameBanana;
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

    private CancellationTokenSource? _stoppingCancellationTokenSource;
    private CancellationTokenSource? _producerWaitingCancellationTokenSource;

    private readonly TimeSpan _waitTime = TimeSpan.FromMinutes(30);

    private readonly BlockingCollection<ModCheckRequest>
        _modCheckRequests = new(new ConcurrentQueue<ModCheckRequest>());

    public event EventHandler<UpdateCheckerEvent>? OnUpdateCheckerEvent;
    public RunningState Status { get; private set; }
    public DateTime? NextRunAt { get; private set; }


    public bool IsReady => Status is RunningState.Stopped or RunningState.Waiting;

    public enum RunningState
    {
        Running,
        Waiting,
        Stopped,
        Error
    }

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

    public Task InitializeAsync()
    {
        _stoppingCancellationTokenSource = new CancellationTokenSource();

        var stoppingToken = _stoppingCancellationTokenSource.Token;


        Task.Factory.StartNew(
            () => CatchAll(() => StartBackgroundChecker(stoppingToken), nameof(StartBackgroundChecker)), stoppingToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);

        Task.Factory.StartNew(() => CatchAll(() => AutoCheckerProducer(stoppingToken), nameof(AutoCheckerProducer)),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        return Task.CompletedTask;
    }

    private async Task CatchAll(Func<Task> func, string methodName)
    {
        try
        {
            await func().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Status = RunningState.Error;
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error occurred while executing {FuncName}", methodName);
            _notificationManager.ShowNotification(
                $"An error occurred in the mod update background checker",
                $"A fatal error occured in the background checker ({methodName} : {e.HResult}). This means that JASM can no longer check for mod updates in the background or manually. Error: {e}",
                TimeSpan.FromSeconds(20));
            Status = RunningState.Error;
        }
    }


    private async Task StartBackgroundChecker(CancellationToken stoppingToken)
    {
        Status = RunningState.Stopped;
        await WaitUntilSkinManagerIsInitialized(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var modCheckRequest = GetNextRequest(stoppingToken);


            var modsToCheck = new List<CharacterSkinEntry>();


            if (modCheckRequest.IsCharacterCheck)
            {
                foreach (var character in modCheckRequest.Characters)
                {
                    var mods = _skinManagerService.GetCharacterModList(character).Mods;
                    modsToCheck.AddRange(mods);
                }
            }
            else if (modCheckRequest.IsModIdCheck)
            {
                foreach (var modId in modCheckRequest.ModIds)
                {
                    var mod = _skinManagerService.CharacterModLists
                        .SelectMany(x => x.Mods)
                        .FirstOrDefault(x => x.Id == modId);
                    if (mod is null)
                    {
                        _logger.Warning("Failed to find mod with id {ModId}", modId);
                        continue;
                    }

                    modsToCheck.Add(mod);
                }
            }

            // Validate mods
            foreach (var skinEntry in modsToCheck.ToArray())
            {
                if (await IsValidForCheckAsync(skinEntry, modCheckRequest.IgnoreLastCheckedTime).ConfigureAwait(false))
                    continue;

                modsToCheck.Remove(skinEntry);
            }


            var requestOperation = new ModCheckOperation(modCheckRequest);
            requestOperation.SetModsToCheck(modsToCheck);


            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                await RunCheckerAsync(requestOperation, stoppingToken).ConfigureAwait(false);
                await FinishedRequestAsync(requestOperation, stoppingToken).ConfigureAwait(false);
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
                NextRunAt = null;
                OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status, NextRunAt));
                break;
            }
        }

        _logger.Debug("ModUpdateAvailableChecker stopped");
    }

    private ModCheckRequest GetNextRequest(CancellationToken stoppingToken)
    {
        var check = _modCheckRequests.Take(stoppingToken);
        Status = RunningState.Running;
        OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status)
        {
            ModCheckRequest = check
        });
        return check;
    }

    private async Task FinishedRequestAsync(ModCheckOperation operation, CancellationToken stoppingToken)
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
            .ConfigureAwait(false);

        stoppingToken.ThrowIfCancellationRequested();

        Status = settings.Enabled ? RunningState.Waiting : RunningState.Stopped;

        OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status, NextRunAt)
        {
            ModCheckRequest = operation.ModCheckRequest,
            ModCheckOperation = operation
        });
    }


    private async Task RunCheckerAsync(ModCheckOperation modCheckOperation, CancellationToken runningCancellationToken)
    {
        if (!modCheckOperation.ModsToCheck.Any())
        {
            if (!modCheckOperation.ModCheckRequest.ScheduledCheck)
                _notificationManager.ShowNotification("No mods to check for updates",
                    $"None of the mods were valid, therefore no check has been performed", TimeSpan.FromSeconds(4));
            return;
        }

        _logger.Information("Checking for mod updates...");
        OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status));

        var stopWatch = Stopwatch.StartNew();
        var anyModsChecked = await CheckForUpdates(modCheckOperation, cancellationToken: runningCancellationToken)
            .ConfigureAwait(false);
        stopWatch.Stop();

        _logger.Debug("Finished checking for mod updates in {Elapsed}", stopWatch.Elapsed);

        if (modCheckOperation.ModCheckRequest.IsCharacterCheck &&
            modCheckOperation.ModCheckRequest.Characters.Length == 1)
        {
            _notificationManager.ShowNotification("Finished checking for mod updates",
                $"Finished checking {modCheckOperation.ModsToCheck.Count} mods for updates for" +
                $" {modCheckOperation.ModCheckRequest.Characters.First().DisplayName}",
                TimeSpan.FromSeconds(4));
        }
        else if (anyModsChecked)
        {
            _notificationManager.ShowNotification("Finished checking for mod updates",
                "Finished checking for mod updates", TimeSpan.FromSeconds(4));
        }
    }

    private async Task<bool> IsValidForCheckAsync(CharacterSkinEntry skinEntry, bool ignoreLastCheckedTime)
    {
        var skinModSettings = await skinEntry.Mod.Settings.TryReadSettingsAsync().ConfigureAwait(false);

        if (skinModSettings is null)
        {
            _logger.Warning("Failed to read mod settings for {ModName}", skinEntry.Mod.FullPath);
            return false;
        }

        if (skinModSettings.ModUrl is null)
        {
            _logger.Verbose("Mod {ModName} has no mod url set", skinEntry.Mod.FullPath);
            return false;
        }

        if (skinModSettings.LastChecked is not null && !ignoreLastCheckedTime &&
            skinModSettings.LastChecked > DateTime.Now.Subtract(TimeSpan.FromMinutes(15)))
        {
            _logger.Verbose("Skipping update check for {ModName}, last update check was {lastUpdate}",
                skinEntry.Mod.FullPath, skinModSettings.LastChecked);
            return false;
        }

        return true;
    }

    private async Task<bool> CheckForUpdates(ModCheckOperation modCheckOperation,
        CancellationToken cancellationToken = default)
    {
        var modEntries = modCheckOperation.ModsToCheck;


        var runningTasks = new List<Task<ModsRetrievedResult>>();
        var queuedMods = new Queue<CharacterSkinEntry>(modEntries);
        var taskToMod = new Dictionary<Task<ModsRetrievedResult>, CharacterSkinEntry>();


        await DeQueueApiTasksAsync(modCheckOperation, queuedMods, runningTasks, taskToMod, cancellationToken)
            .ConfigureAwait(false);

        if (runningTasks.Count == 0)
        {
            _logger.Debug("No mods to check for updates");
            return false;
        }


        while (runningTasks.Count != 0)
        {
            var finishedTask = await Task.WhenAny(runningTasks).ConfigureAwait(false);
            runningTasks.Remove(finishedTask);
            var characterSkinEntry = taskToMod[finishedTask];
            try
            {
                var result = await finishedTask.ConfigureAwait(false);
                await characterSkinEntry.Mod.Settings.SetLastCheckedTimeAsync(DateTime.Now).ConfigureAwait(false);
                if (result.AnyNewMods)
                {
                    await AddModNotifications(characterSkinEntry, result).ConfigureAwait(false);
                    _logger.Information("New or updated mods are available for {ModName}", characterSkinEntry.Mod.Name);
                }
            }
            catch (InvalidGameBananaUrlException e)
            {
                _logger.Debug(e, "Invalid GameBanana url for {ModName}", characterSkinEntry.Mod.FullPath);
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error occurred while checking for mod update for {ModName}",
                    characterSkinEntry.Mod.FullPath);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await DeQueueApiTasksAsync(modCheckOperation, queuedMods, runningTasks, taskToMod, cancellationToken)
                .ConfigureAwait(false);
        }

        modCheckOperation.RequestFinished();
        return true;
    }

    private async Task DeQueueApiTasksAsync(ModCheckOperation modCheckOperation,
        Queue<CharacterSkinEntry> queuedMods, List<Task<ModsRetrievedResult>> runningTasks,
        Dictionary<Task<ModsRetrievedResult>, CharacterSkinEntry> taskToMod, CancellationToken cancellationToken)
    {
        const int maxQueueSize = 20;

        while (runningTasks.Count < maxQueueSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (queuedMods.Count == 0)
                break;

            var characterSkinEntry = queuedMods.Dequeue();

            var modSettings = await characterSkinEntry.Mod.Settings
                .ReadSettingsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (modSettings.ModUrl is null)
                continue;

            var ignoreCache = modCheckOperation.ModCheckRequest.IgnoreLastCheckedTime;

            var fetchTask = _gameBananaService.GetAvailableModFiles(characterSkinEntry.Id, ignoreCache: ignoreCache,
                cancellationToken);

            runningTasks.Add(fetchTask);
            taskToMod.Add(fetchTask, characterSkinEntry);
        }
    }

    private Task AddModNotifications(CharacterSkinEntry characterSkinEntry, ModsRetrievedResult result)
    {
        var modSettingsResult = characterSkinEntry.Mod.Settings.GetSettingsLegacy();

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

    public void CheckNow(ModCheckRequest? checkRequest = null)
    {
        if (checkRequest is null)
        {
            _modCheckRequests.Add(
                ModCheckRequest.ForModId(
                    _skinManagerService.CharacterModLists.SelectMany(chm => chm.Mods.Select(ske => ske.Mod.Id))));
            return;
        }

        _modCheckRequests.Add(checkRequest);
    }

    public void CancelAndStop()
    {
        _stoppingCancellationTokenSource?.Cancel();
        _stoppingCancellationTokenSource = null;
        _producerWaitingCancellationTokenSource?.Cancel();
        _producerWaitingCancellationTokenSource = null;
    }

    public async Task DisableAutoCheckerAsync()
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
            .ConfigureAwait(false);
        settings.Enabled = false;
        await _localSettingsService.SaveSettingAsync(BackGroundModCheckerSettings.Key, settings).ConfigureAwait(false);

        _logger.Information("Disabled background mod update checker");
        ResetProducer();
        Status = RunningState.Stopped;
        NextRunAt = null;
        OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status, NextRunAt));
    }

    public async Task EnableAutoCheckerAsync()
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
            .ConfigureAwait(false);
        settings.Enabled = true;
        await _localSettingsService.SaveSettingAsync(BackGroundModCheckerSettings.Key, settings).ConfigureAwait(false);

        _logger.Information("Enabled background mod update checker");
        ResetProducer();
    }


    private async Task AutoCheckerProducer(CancellationToken stoppingToken)
    {
        _producerWaitingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await WaitUntilSkinManagerIsInitialized(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested && Status != RunningState.Error)
        {
            var settings = await _localSettingsService
                .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
                .ConfigureAwait(false);

            stoppingToken.ThrowIfCancellationRequested();

            while (!settings.Enabled)
            {
                NextRunAt = null;
                await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken).ConfigureAwait(false);
                settings = await _localSettingsService
                    .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
                    .ConfigureAwait(false);
            }

            var check = ModCheckRequest.ForModId(
                _skinManagerService.CharacterModLists.SelectMany(chm => chm.Mods.Select(ske => ske.Mod.Id)));
            check.ScheduledCheck = true;

            if (Status == RunningState.Error)
                break;

            _modCheckRequests.Add(check,
                stoppingToken);

            NextRunAt = DateTime.Now + _waitTime;
            OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status, NextRunAt));
            try
            {
                await Task.Delay(_waitTime, _producerWaitingCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException e)
            {
            }
        }

        _logger.Debug("AutoCheckerProducer stopped");
    }

    private void ResetProducer()
    {
        var oldCt = _producerWaitingCancellationTokenSource;
        oldCt?.Cancel();
        _producerWaitingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _stoppingCancellationTokenSource?.Token ?? CancellationToken.None);
        oldCt?.Dispose();
    }

    private async Task WaitUntilSkinManagerIsInitialized(CancellationToken stoppingToken)
    {
        while (!_skinManagerService.IsInitialized)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
        }
    }
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
    public ModCheckRequest? ModCheckRequest { get; init; }
    public ModCheckOperation? ModCheckOperation { get; init; }

    public UpdateCheckerEvent(RunningState state, DateTime? nextRunAt = null)
    {
        State = state;
        NextRunAt = nextRunAt;
    }
}

public class ModCheckOperation
{
    public Guid Id => ModCheckRequest.Id;
    public ModCheckRequest ModCheckRequest { get; }

    public DateTime? RequestFinishedAt { get; private set; }


    private List<CharacterSkinEntry> _modsToCheck = new();

    public IReadOnlyList<CharacterSkinEntry> ModsToCheck => _modsToCheck.AsReadOnly();


    public ModCheckOperation(ModCheckRequest request)
    {
        ModCheckRequest = request;
    }

    public void SetModsToCheck(IEnumerable<CharacterSkinEntry> mods)
    {
        if (_modsToCheck.Any() || RequestFinishedAt is not null)
            throw new InvalidOperationException("Request is already finished");
        _modsToCheck = new List<CharacterSkinEntry>(mods);
    }

    public void RequestFinished()
    {
        if (RequestFinishedAt is not null)
            throw new InvalidOperationException("Request is already finished");
        RequestFinishedAt = DateTime.Now;
    }
}

public class ModCheckRequest
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime RequestedAt { get; } = DateTime.Now;
    public IModdableObject[]? Characters { get; private init; }
    public Guid[]? ModIds { get; private init; }
    public bool IgnoreLastCheckedTime { get; private set; }

    public bool ScheduledCheck { get; set; }


    [MemberNotNullWhen(true, nameof(Characters))]
    public bool IsCharacterCheck => Characters is not null;

    [MemberNotNullWhen(true, nameof(ModIds))]
    public bool IsModIdCheck => ModIds is not null;

    private ModCheckRequest()
    {
    }

    public static ModCheckRequest ForCharacter(IModdableObject character)
    {
        return new ModCheckRequest()
        {
            Characters = new[] { character }
        };
    }

    public static ModCheckRequest ForModId(Guid modId)
    {
        return new ModCheckRequest()
        {
            ModIds = new[] { modId }
        };
    }

    public static ModCheckRequest ForModId(IEnumerable<Guid> modIds)
    {
        return new ModCheckRequest()
        {
            ModIds = modIds.ToArray()
        };
    }

    public ModCheckRequest WithIgnoreLastChecked(bool ignoreMinimumWaitTime = true)
    {
        IgnoreLastCheckedTime = ignoreMinimumWaitTime;
        return this;
    }
}