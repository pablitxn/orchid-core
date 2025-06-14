using Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
///     Simple cache service combining memory and distributed stores.
/// </summary>
public sealed class CacheStore(IMemoryCache memoryCache, IDistributedCache distributedCache, ILogger<CacheStore> logger)
    : ICacheStore
{
    private readonly IDistributedCache _distributed = distributedCache;
    private readonly ILogger<CacheStore> _logger = logger;
    private readonly IMemoryCache _memory = memoryCache;

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        if (_memory.TryGetValue<string>(key, out var cached))
            return cached;
        try
        {
            var value = await _distributed.GetStringAsync(key, cancellationToken);
            if (value is not null)
                _memory.Set(key, value, TimeSpan.FromHours(6));
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache key '{CacheKey}' from distributed cache", key);
            return null;
        }
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        if (value.Length == 0)
            throw new ArgumentException("Cache value cannot be empty.", nameof(value));
        _memory.Set(key, value, slidingExpiration ?? TimeSpan.FromHours(6));
        try
        {
            await _distributed.SetStringAsync(key, value, new DistributedCacheEntryOptions
            {
                SlidingExpiration = slidingExpiration ?? TimeSpan.FromHours(24)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key '{CacheKey}' in distributed cache", key);
        }
    }

    /// <summary>
    ///     Removes a cached entry for the specified key.
    /// </summary>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        _memory.Remove(key);
        try
        {
            await _distributed.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key '{CacheKey}' from distributed cache", key);
        }
    }
}