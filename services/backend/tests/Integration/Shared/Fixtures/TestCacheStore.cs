using System.Collections.Concurrent;
using Application.Interfaces;

namespace Integration.Shared.Fixtures;

/// <summary>
/// In-memory cache store implementation for integration testing.
/// Thread-safe and supports all ICacheStore operations.
/// </summary>
public class TestCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<string, (string Value, DateTime? Expiry)> _cache = new();

    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.Expiry == null || entry.Expiry > DateTime.UtcNow)
            {
                return Task.FromResult<string?>(entry.Value);
            }

            _cache.TryRemove(key, out _);
        }

        return Task.FromResult<string?>(null);
    }

    public Task SetStringAsync(string key, string value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        var expiryTime = slidingExpiration.HasValue ? DateTime.UtcNow.Add(slidingExpiration.Value) : (DateTime?)null;
        _cache[key] = (value, expiryTime);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks if a key exists and is not expired.
    /// </summary>
    public bool Exists(string key)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.Expiry == null || entry.Expiry > DateTime.UtcNow)
            {
                return true;
            }

            _cache.TryRemove(key, out _);
        }

        return false;
    }

    /// <summary>
    /// Clears all entries from the cache. Useful for test cleanup.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }
}
