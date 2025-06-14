using System.Collections.Concurrent;
using System.Text.Json;
using Application.Interfaces;

namespace CreditSystem.IntegrationTests;

/// <summary>
/// Simple in-memory implementation of ICacheStore for testing purposes.
/// </summary>
public class TestCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<string, (string Value, DateTime? Expiry)> _cache = new();

    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.Expiry.HasValue || entry.Expiry.Value > DateTime.UtcNow)
            {
                return Task.FromResult<string?>(entry.Value);
            }
            
            // Remove expired entry
            _cache.TryRemove(key, out _);
        }
        
        return Task.FromResult<string?>(null);
    }

    public Task SetStringAsync(string key, string value, TimeSpan? slidingExpiration = null, 
        CancellationToken cancellationToken = default)
    {
        var expiry = slidingExpiration.HasValue 
            ? DateTime.UtcNow.Add(slidingExpiration.Value) 
            : (DateTime?)null;
        
        _cache[key] = (value, expiry);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var stringValue = await GetStringAsync(key, cancellationToken);
        if (stringValue == null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(stringValue);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan slidingExpiration, 
        CancellationToken cancellationToken = default) where T : class
    {
        var stringValue = JsonSerializer.Serialize(value);
        await SetStringAsync(key, stringValue, slidingExpiration, cancellationToken);
    }
}