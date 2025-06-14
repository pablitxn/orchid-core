using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Application.Interfaces;

/// <summary>
///     Extension methods providing typed access to <see cref="IDistributedCache" />.
/// </summary>
public static class DistributedCacheExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task SetAsync<T>(this IDistributedCache cache, string key, T value,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, Options);
        await cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(1)
        }, cancellationToken);
    }

    public static async Task<T?> GetAsync<T>(this IDistributedCache cache, string key,
        CancellationToken cancellationToken = default)
    {
        var json = await cache.GetStringAsync(key, cancellationToken);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, Options);
    }
}
