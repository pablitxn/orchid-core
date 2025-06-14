using System.Text.Json;

namespace Application.Interfaces;

/// <summary>
///     Extension methods providing typed access to <see cref="ICacheStore" />.
/// </summary>
public static class CacheStoreExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task SetAsync<T>(
        this ICacheStore cache,
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default
    )
    {
        var json = JsonSerializer.Serialize(value, Options);
        await cache.SetStringAsync(key, json, slidingExpiration, cancellationToken);
    }

    public static async Task<T?> GetAsync<T>(this ICacheStore cache, string key,
        CancellationToken cancellationToken = default)
    {
        var json = await cache.GetStringAsync(key, cancellationToken);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, Options);
    }
}