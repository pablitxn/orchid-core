namespace Application.Interfaces;

/// <summary>
///     Generic cache store for simple string values.
/// </summary>
public interface ICacheStore
{
    /// <summary>
    ///     Retrieves a cached string value or null if missing.
    /// </summary>
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stores a string value with optional expiration.
    /// </summary>
    Task SetStringAsync(string key, string value, TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a cached entry for the specified key.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}