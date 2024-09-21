using System.Collections.Concurrent;

namespace GIMI_ModManager.Core.Services.GameBanana;

internal sealed class ApiGameBananaCache
{
    private readonly ConcurrentDictionary<string, CacheEntry<object>> _cache = new();

    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    internal ApiGameBananaCache()
    {
    }

    internal ApiGameBananaCache(TimeSpan cacheDuration)
    {
        _cacheDuration = cacheDuration;
    }

    public T? Get<T>(string key) where T : class
    {
        ClearExpiredEntries();

        key = CreateKey(key, typeof(T));

        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired)
            {
                return (T)entry.Value;
            }

            _cache.TryRemove(key, out _);
        }

        return null;
    }

    public T[] GetAll<T>() where T : class
    {
        ClearExpiredEntries();

        return _cache
            .Where(x => x.Key.StartsWith($"{typeof(T).Name}_"))
            .Select(x => (T)x.Value.Value)
            .ToArray();
    }


    public void Set<T>(string key, T value, TimeSpan? cacheDuration = null) where T : class
    {
        ClearExpiredEntries();
        key = CreateKey(key, typeof(T));

        _cache[key] = new CacheEntry<object>(value, cacheDuration ?? _cacheDuration);
    }


    public void ClearExpiredEntries()
    {
        foreach (var (key, entry) in _cache.ToArray())
        {
            if (entry.IsExpired)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    public void ClearAllEntries()
    {
        _cache.Clear();
    }

    private static string CreateKey(string key, Type type) => $"{type.Name}_{key}";
}

internal sealed class CacheEntry<T>
{
    public T Value { get; }
    public DateTime Creation { get; }
    public DateTime Expiration => Creation.Add(CacheDuration);
    public bool IsExpired => DateTime.Now > Expiration;

    public TimeSpan CacheDuration { get; }

    public CacheEntry(T value, TimeSpan cacheDuration)
    {
        Value = value;
        Creation = DateTime.Now;
        CacheDuration = cacheDuration;
    }
}