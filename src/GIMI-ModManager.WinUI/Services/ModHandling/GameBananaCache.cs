using System.Collections.Concurrent;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public class GameBananaCache
{
    private readonly ILogger _logger;
    private readonly ISkinManagerService _skinManagerService;

    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

    private readonly TimeSpan _minTimeBetweenChecks = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<Guid, ModCacheInfo> _cache = new();


    public GameBananaCache(ILogger logger, ISkinManagerService skinManagerService)
    {
        _skinManagerService = skinManagerService;
        _logger = logger.ForContext<GameBananaCache>();
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
        {
            throw new NotImplementedException();
        }

        var modSettings = await mod.Settings.ReadSettingsAsync();

        if (modSettings.ModUrl is null)
        {
            throw new NotImplementedException();
        }

        var worker = App.GetService<IModUpdateChecker>();

        var result = await worker.GetModPageDataAsync(modSettings.ModUrl, cancellationToken);


        CacheModInfo(modId, result);

        return result;
    }

    // Get info about new or updated mods

    public async Task<ModsRetrievedResult> GetAvailableMods(Guid modId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(modId, out var cacheInfo))
        {
            if (cacheInfo.Result is not null && cacheInfo.Result.CheckTime + _cacheDuration > DateTime.Now)
                return cacheInfo.Result;
        }


        var mod = _skinManagerService.GetModById(modId);
        if (mod is null)
        {
            throw new NotImplementedException();
        }

        var modSettings = await mod.Settings.ReadSettingsAsync();

        if (modSettings.ModUrl is null)
        {
            throw new NotImplementedException();
        }

        if (modSettings.LastChecked is not null && modSettings.LastChecked + _minTimeBetweenChecks > DateTime.Now)
        {
            await mod.Settings.SaveSettingsAsync(modSettings.DeepCopyWithProperties(newLastChecked: DateTime.Now));
            modSettings = await mod.Settings.ReadSettingsAsync();
            if (modSettings.ModUrl is null)
            {
                throw new NotImplementedException();
            }
        }

        var worker = App.GetService<IModUpdateChecker>();

        var result = await worker.CheckForUpdatesAsync(modSettings.ModUrl, modSettings.LastChecked ?? DateTime.MinValue,
            cancellationToken);

        CacheRetrievedMods(modId, result);
        return result;
    }


    public void CacheRetrievedMods(Guid modId, ModsRetrievedResult result)
    {
        _cache.AddOrUpdate(modId, new ModCacheInfo
        {
            ModPageInfo = null,
            Result = result
        }, (_, oldValue) => new ModCacheInfo
        {
            ModPageInfo = oldValue.ModPageInfo,
            Result = result
        });
    }

    public void CacheModInfo(Guid modId, ModPageDataResult result)
    {
        _cache.AddOrUpdate(modId, new ModCacheInfo
        {
            ModPageInfo = result,
            Result = null
        }, (_, oldValue) => new ModCacheInfo
        {
            ModPageInfo = result,
            Result = oldValue.Result
        });
    }
}

public class ModCacheInfo
{
    public ModPageDataResult? ModPageInfo { get; set; }
    public ModsRetrievedResult? Result { get; set; }
}