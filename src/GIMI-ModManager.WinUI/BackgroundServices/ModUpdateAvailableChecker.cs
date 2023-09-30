using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace GIMI_ModManager.WinUI.BackgroundServices;

public sealed class ModUpdateAvailableChecker : BackgroundService
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILogger _logger;
    private readonly ModNotificationManager _modNotificationManager;


    private static CancellationTokenSource _waiterCancellationTokenSource = null!;
    private static CancellationTokenSource _runningCancellationTokenSource = null!;

    private static CancellationToken _waiterCancellationToken = default;
    private static CancellationToken _runningCancellationToken = default;
    private static bool _isRunning = false;
    private static bool _forceUpdate = false;

    public ModUpdateAvailableChecker(ISkinManagerService skinManagerService, ILogger logger,
        ModNotificationManager modNotificationManager)
    {
        _skinManagerService = skinManagerService;
        _modNotificationManager = modNotificationManager;
        _logger = logger.ForContext<ModUpdateAvailableChecker>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _waiterCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _runningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            if (!_skinManagerService.IsInitialized)
            {
                SpinWait.SpinUntil(() =>
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    return _skinManagerService.IsInitialized;
                });
                continue;
            }

            await Task.Delay(2000, stoppingToken);
            _logger.Information("Checking for mod updates...");

            try
            {
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
        }


        _waiterCancellationToken = _waiterCancellationTokenSource.Token;
        await WaitForNextCheck(_waiterCancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForNextCheck(CancellationToken token)
    {
        try
        {
            _logger.Information("Waiting for next mod check at {nextCheck}", DateTime.Now.AddMinutes(5));
            await Task.Delay(TimeSpan.FromMinutes(5), token);
        }
        catch (TaskCanceledException)
        {
        }
    }


    private async Task CheckForUpdates(bool forceUpdate = false, CancellationToken cancellationToken = default)
    {
        var characterModLists = _skinManagerService.CharacterModLists;
        var modEntries = characterModLists.SelectMany(x => x.Mods);

        var modEntriesWithUrls = new List<CharacterSkinEntry>();
        foreach (var characterSkinEntry in modEntries)
        {
            await characterSkinEntry.Mod.ReadSkinModSettings(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(characterSkinEntry.Mod?.CachedSkinModSettings?.ModUrl)) continue;

            if (!Uri.TryCreate(characterSkinEntry.Mod?.CachedSkinModSettings?.ModUrl, UriKind.Absolute,
                    out var _)) continue;

            modEntriesWithUrls.Add(characterSkinEntry);
        }

        var tasks = new List<Task<ModsRetrievedResult>>();

        foreach (var mod in modEntriesWithUrls.Select(m => m.Mod))
        {
            if (mod.CachedSkinModSettings?.ModUrl is null) continue;

            if (mod.CachedSkinModSettings.LastChecked is not null && (mod.CachedSkinModSettings.LastChecked >
                    DateTime.Now.Subtract(TimeSpan.FromMinutes(15)) || !forceUpdate))
            {
                _logger.Debug("Skipping update check for {modName}, last update check was {lastUpdate}", mod.Name,
                    mod.CachedSkinModSettings.LastChecked);
                continue;
            }

            var checker = App.GetService<IModUpdateChecker>();

            var url = new Uri(mod.CachedSkinModSettings.ModUrl);


            var fetchTask = Task.Run(async () => await checker.CheckForUpdatesAsync(url,
                mod.CachedSkinModSettings.LastChecked ?? DateTime.MinValue,
                cancellationToken), cancellationToken);

            tasks.Add(fetchTask);
        }

        var results = await Task.WhenAll(tasks);

        await AddModNotifications(results, modEntriesWithUrls).ConfigureAwait(false);
    }

    private async Task AddModNotifications(IEnumerable<ModsRetrievedResult> results,
        IReadOnlyCollection<CharacterSkinEntry> modEntriesWithUrls)
    {
        foreach (var modsRetrievedResult in results.Where(result => result.AnyNewMods))
        {
            if (modEntriesWithUrls.FirstOrDefault(x =>
                    new Uri(x.Mod.CachedSkinModSettings?.ModUrl!) == modsRetrievedResult.SitePageUrl)
                is not { } modEntry)
            {
                _logger.Warning("Failed to find mod entry for {url}", modsRetrievedResult.SitePageUrl);
                continue;
            }

            var modNotification = new SkinModNotification(modEntry.Mod, "Possible updates available",
                AttentionType.UpdateAvailable, modEntry.ModList.Character.Id);


            await _modNotificationManager.AddModNotification(modNotification);
        }
    }


    public static void CheckNow(bool runNow = true, bool forceUpdate = false,
        CancellationToken cancellationToken = default)
    {
        if (runNow)
        {
            _waiterCancellationTokenSource.Cancel();
            _runningCancellationTokenSource.Cancel();
        }

        SpinWait.SpinUntil(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return !_isRunning;
        });

        _forceUpdate = forceUpdate;
    }
}