using System.Collections.Concurrent;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public class GameBananaService
{
    private readonly ILogger _logger;
    private readonly ISkinManagerService _skinManagerService;

    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

    private readonly TimeSpan _minTimeBetweenChecks = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<Guid, ModCacheInfo> _cache = new();


    public GameBananaService(ILogger logger, ISkinManagerService skinManagerService)
    {
        _skinManagerService = skinManagerService;
        _logger = logger.ForContext<GameBananaService>();
    }


    public async Task<ModPageDataResult> GetModInfoAsync(Guid modId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(modId, out var cacheInfo))
        {
            if (cacheInfo.ModPageInfo is not null && cacheInfo.ModPageInfo.CheckTime + _cacheDuration > DateTime.Now)
                return cacheInfo.ModPageInfo;
        }

        var mod = _skinManagerService.GetModById(modId);
        if (mod is null)
            throw new InvalidOperationException($"Mod with id {modId} not found");

        var modSettings = await mod.Settings.ReadSettingsAsync();
        if (modSettings.ModUrl is null)
            throw new InvalidOperationException("Mod url is null");


        var client = App.GetService<IModUpdateChecker>();

        var result = await client.GetModPageDataAsync(modSettings.ModUrl, cancellationToken);


        CacheModInfo(modId, result);

        return result;
    }

    // Get info about new or updated mods

    public async Task<ModsRetrievedResult> GetAvailableMods(Guid modId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(modId, out var cacheInfo))
        {
            if (cacheInfo.ModsResult is not null && cacheInfo.ModsResult.CheckTime + _cacheDuration > DateTime.Now)
                return cacheInfo.ModsResult;
        }


        var mod = _skinManagerService.GetModById(modId);
        if (mod is null)
            throw new InvalidOperationException($"Mod with id {modId} not found");


        var modSettings = await mod.Settings.ReadSettingsAsync();

        if (modSettings.ModUrl is null)
            throw new InvalidOperationException("Mod url is null");


        var client = App.GetService<IModUpdateChecker>();

        var result = await client.CheckForUpdatesAsync(modSettings.ModUrl, modSettings.LastChecked ?? DateTime.Now,
            cancellationToken);

        CacheRetrievedMods(modId, result);
        return result;
    }


    public void CacheRetrievedMods(Guid modId, ModsRetrievedResult result)
    {
        _cache.AddOrUpdate(modId, new ModCacheInfo
        {
            ModPageInfo = null,
            ModsResult = result
        }, (_, oldValue) => new ModCacheInfo
        {
            ModPageInfo = oldValue.ModPageInfo,
            ModsResult = result
        });
    }

    public void CacheModInfo(Guid modId, ModPageDataResult result)
    {
        _cache.AddOrUpdate(modId, new ModCacheInfo
        {
            ModPageInfo = result,
            ModsResult = null
        }, (_, oldValue) => new ModCacheInfo
        {
            ModPageInfo = result,
            ModsResult = oldValue.ModsResult
        });
    }
}

public class ModCacheInfo
{
    public ModPageDataResult? ModPageInfo { get; set; }
    public ModsRetrievedResult? ModsResult { get; set; }
}