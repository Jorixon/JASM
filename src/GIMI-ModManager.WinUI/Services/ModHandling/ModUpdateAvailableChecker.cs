using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.GamesService.Interfaces;
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

    private CancellationTokenSource? _stoppingCancellationTokenSource;

    private bool _isRunning;

    private readonly TimeSpan _waitTime = TimeSpan.FromMinutes(30);

    public event EventHandler<UpdateCheckerEvent>? OnUpdateCheckerEvent;
    public RunningState Status { get; private set; }
    public DateTime? NextRunAt { get; private set; }

    private readonly BlockingCollection<ModCheckRequest>
        _modCheckRequests = new(new ConcurrentQueue<ModCheckRequest>());

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

        Task.Run(() => StartBackgroundChecker(stoppingToken), CancellationToken.None);
        Task.Run(() => AutoCheckerProducer(stoppingToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task StartBackgroundChecker(CancellationToken stoppingToken)
    {
        await WaitUntilSkinManagerIsInitialized(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var modCheckRequest = _modCheckRequests.Take(stoppingToken);


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
            foreach (var skinEntry in modsToCheck)
            {
                if (await IsValidForCheckAsync(skinEntry, modCheckRequest.IgnoreLastCheckedTime))
                    continue;

                modsToCheck.Remove(skinEntry);
            }

            modCheckRequest.SetModsToCheck(modsToCheck);

            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                await RunCheckerAsync(modCheckRequest, stoppingToken).ConfigureAwait(false);
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

            Status = RunningState.Stopped;
        }

        _logger.Debug("ModUpdateAvailableChecker stopped");
    }

    private async Task RunCheckerAsync(ModCheckRequest modCheckRequest, CancellationToken runningCancellationToken)
    {
        try
        {
            _isRunning = true;
            _logger.Information("Checking for mod updates...");
            Status = RunningState.Running;
            OnUpdateCheckerEvent?.Invoke(this, new UpdateCheckerEvent(Status));

            var stopWatch = Stopwatch.StartNew();
            await CheckForUpdates(modCheckRequest, cancellationToken: runningCancellationToken).ConfigureAwait(false);
            stopWatch.Stop();

            _logger.Debug("Finished checking for mod updates in {Elapsed}", stopWatch.Elapsed);
            _notificationManager.ShowNotification("Finished checking for mod updates",
                $"Finished checking for mod updates", TimeSpan.FromSeconds(4));
        }

        finally
        {
            _isRunning = false;
        }
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

    private async Task<bool> IsValidForCheckAsync(CharacterSkinEntry skinEntry, bool ignoreLastCheckedTime)
    {
        var skinModSettings = await skinEntry.Mod.Settings.TryReadSettingsAsync();

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

    private async Task CheckForUpdates(ModCheckRequest modCheckRequest, CancellationToken cancellationToken = default)
    {
        var modEntries = modCheckRequest.ModsToCheck;


        var tasks = new List<Task<ModsRetrievedResult>>();
        var taskToMod = new Dictionary<Task<ModsRetrievedResult>, CharacterSkinEntry>();
        foreach (var characterSkinEntry in modEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var modSettings = await characterSkinEntry.Mod.Settings.ReadSettingsAsync().ConfigureAwait(false);

            if (modSettings.ModUrl is null)
                continue;

            var fetchTask = Task.Run(
                () => _gameBananaService.GetAvailableMods(characterSkinEntry.Id, cancellationToken),
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
                await characterSkinEntry.Mod.Settings.SetLastCheckedTimeAsync(DateTime.Now).ConfigureAwait(false);
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

    public void CheckNow(IEnumerable<Guid>? checkOnlyMods = null, bool forceUpdate = false,
        CancellationToken cancellationToken = default)
    {
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
        _logger.Information("Disabled background mod update checker");
    }

    public async Task StartBackgroundCheckerAsync()
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
            .ConfigureAwait(false);
        settings.Enabled = true;
        await _localSettingsService.SaveSettingAsync(BackGroundModCheckerSettings.Key, settings).ConfigureAwait(false);
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

    private async Task AutoCheckerProducer(CancellationToken stoppingToken)
    {
        await WaitUntilSkinManagerIsInitialized(stoppingToken).ConfigureAwait(false);


        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = await _localSettingsService
                .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
                .ConfigureAwait(false);
            stoppingToken.ThrowIfCancellationRequested();

            while (!settings.Enabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken).ConfigureAwait(false);
                settings = await _localSettingsService
                    .ReadOrCreateSettingAsync<BackGroundModCheckerSettings>(BackGroundModCheckerSettings.Key)
                    .ConfigureAwait(false);
            }

            _modCheckRequests.Add(
                ModCheckRequest.CheckForModId(
                    _skinManagerService.CharacterModLists.SelectMany(chm => chm.Mods.Select(ske => ske.Mod.Id))),
                stoppingToken);


            await Task.Delay(_waitTime, stoppingToken).ConfigureAwait(false);
        }
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

    public UpdateCheckerEvent(RunningState state, DateTime? nextRunAt = null)
    {
        State = state;
        NextRunAt = nextRunAt;
    }
}

public class ModCheckRequest
{
    public bool IgnoreLastCheckedTime { get; private set; }
    public DateTime RequestedAt { get; } = DateTime.Now;
    public DateTime? RequestFinishedAt { get; private set; }
    public IModdableObject[]? Characters { get; private init; }
    public Guid[]? ModIds { get; private init; }

    private List<CharacterSkinEntry> _modsToCheck = new();

    public IReadOnlyList<CharacterSkinEntry> ModsToCheck => _modsToCheck;


    [MemberNotNullWhen(true, nameof(Characters))]
    public bool IsCharacterCheck => Characters is not null;

    [MemberNotNullWhen(true, nameof(ModIds))]
    public bool IsModIdCheck => ModIds is not null;

    public static ModCheckRequest CheckForCharacter(IModdableObject character)
    {
        return new ModCheckRequest()
        {
            Characters = new[] { character }
        };
    }

    public static ModCheckRequest CheckForModId(Guid modId)
    {
        return new ModCheckRequest()
        {
            ModIds = new[] { modId }
        };
    }

    public static ModCheckRequest CheckForModId(IEnumerable<Guid> modIds)
    {
        return new ModCheckRequest()
        {
            ModIds = modIds.ToArray()
        };
    }


    public ModCheckRequest WithIgnoreLastChecked()
    {
        IgnoreLastCheckedTime = true;
        return this;
    }

    public void SetModsToCheck(IEnumerable<CharacterSkinEntry> mods)
    {
        _modsToCheck = new List<CharacterSkinEntry>(mods);
    }
}

public enum RequestStatus
{
    Pending,
    Running,
    Finished,
    Error
}