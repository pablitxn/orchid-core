using Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Cache;

/// <summary>
///     Lightweight <see cref="ICacheStore" /> implementation backed by
///     <see cref="IMemoryCache" />. Useful for development or unit tests.
/// </summary>
public sealed class InMemoryCacheService(IMemoryCache memoryCache) : ICacheStore
{
    private readonly IMemoryCache _cache = memoryCache;

    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));

        return Task.FromResult(_cache.TryGetValue(key, out string? value) ? value : null);
    }

    public Task SetStringAsync(string key, string value, TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        if (value.Length == 0)
            throw new ArgumentException("Cache value cannot be empty.", nameof(value));

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            // Use sliding expiration for cache entries
            SlidingExpiration = slidingExpiration ?? TimeSpan.FromHours(1)
        };
        _cache.Set(key, value, cacheEntryOptions);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));

        _cache.Remove(key);
        return Task.CompletedTask;
    }
}