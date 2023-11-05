using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public sealed class ModUpdateAvailableChecker
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly GameBananaCache _gameBananaCache;
    private readonly ILogger _logger;
    private readonly ModNotificationManager _modNotificationManager;

    private readonly CancellationTokenSource _stoppingCancellationTokenSource = new();

    private CancellationTokenSource _waiterCancellationTokenSource = null!;
    private CancellationTokenSource _runningCancellationTokenSource = null!;

    private CancellationToken _waiterCancellationToken = default;
    private CancellationToken _runningCancellationToken = default;
    private bool _isRunning = false;
    private bool _forceUpdate = false;

    private List<Guid>? _checkOnlyMods;
    private TimeSpan _waitTime = TimeSpan.FromMinutes(30);

    public ModUpdateAvailableChecker(ISkinManagerService skinManagerService, ILogger logger,
        ModNotificationManager modNotificationManager, GameBananaCache gameBananaCache)
    {
        _skinManagerService = skinManagerService;
        _modNotificationManager = modNotificationManager;
        _gameBananaCache = gameBananaCache;
        _logger = logger.ForContext<ModUpdateAvailableChecker>();
    }

    public async Task InitializeAsync()
    {
        var stoppingToken = _stoppingCancellationTokenSource.Token;
        while (!stoppingToken.IsCancellationRequested)
        {
            _waiterCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _runningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            if (!_skinManagerService.IsInitialized)
                SpinWait.SpinUntil(() => _skinManagerService.IsInitialized || stoppingToken.IsCancellationRequested);


            try
            {
                await Task.Delay(2000, stoppingToken);
                await StartBackgroundService();
            }
            catch (TaskCanceledException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to check for mod updates");
            }
            finally
            {
                _waiterCancellationTokenSource?.Dispose();
                _runningCancellationTokenSource?.Dispose();
                _waiterCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _runningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            }
        }

        _logger.Debug("ModUpdateAvailableChecker stopped");
    }

    private async Task StartBackgroundService()
    {
        try
        {
            _isRunning = true;
            _runningCancellationToken = _runningCancellationTokenSource.Token;
            _logger.Information("Checking for mod updates...");
            await CheckForUpdates(_forceUpdate, cancellationToken: _runningCancellationToken);
        }

        finally
        {
            _forceUpdate = false;
            _isRunning = false;
            _checkOnlyMods = null;
        }


        _waiterCancellationToken = _waiterCancellationTokenSource.Token;
        await WaitForNextCheck(_waiterCancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForNextCheck(CancellationToken token)
    {
        try
        {
            _logger.Information("Next update check will run at {WaitTime}", DateTime.Now.Add(_waitTime));
            await Task.Delay(_waitTime, token);
        }
        catch (TaskCanceledException)
        {
        }
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
            var modSettings = await mod.Settings.ReadSettingsAsync();

            if (modSettings.ModUrl is null)
                continue;

            if (modSettings.LastChecked is not null && (modSettings.LastChecked >
                    DateTime.Now.Subtract(TimeSpan.FromMinutes(15)) && !forceUpdate))
            {
                _logger.Debug("Skipping update check for {modName}, last update check was {lastUpdate}", mod.Name,
                    modSettings.LastChecked);
                continue;
            }

            var checker = App.GetService<IModUpdateChecker>();


            var fetchTask = Task.Run(() => checker.CheckForUpdatesAsync(modSettings.ModUrl,
                modSettings.LastChecked ?? DateTime.MinValue,
                cancellationToken), cancellationToken);

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
            var finishedTask = await Task.WhenAny(tasks);
            tasks.Remove(finishedTask);
            var characterSkinEntry = taskToMod[finishedTask];
            try
            {
                var result = await finishedTask;
                await UpdateLastChecked(characterSkinEntry.Mod, result);
                _gameBananaCache.CacheRetrievedMods(characterSkinEntry.Mod.Id, result);
                if (result.AnyNewMods)
                {
                    await AddModNotifications(characterSkinEntry, result);
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
            Message = $"New or updated mods are available for {characterSkinEntry.Mod.GetNameWithoutDisabledPrefix()}"
        };

        return _modNotificationManager.AddModNotification(modNotification, persistent: true);
    }

    private bool _isStartingUpdate = false;

    public void CheckNow(bool forceUpdate = false, IEnumerable<Guid>? checkOnlyMods = null,
        CancellationToken cancellationToken = default)
    {
        if (_isStartingUpdate)
            return;
        _isStartingUpdate = true;
        _waiterCancellationTokenSource.Cancel();
        _runningCancellationTokenSource.Cancel();

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
        _stoppingCancellationTokenSource.Cancel();
        _stoppingCancellationTokenSource.Dispose();
    }
}