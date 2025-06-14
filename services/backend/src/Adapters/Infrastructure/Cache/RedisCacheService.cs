using Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using IDatabase = StackExchange.Redis.IDatabase;

namespace Infrastructure.Cache;

/// <summary>
///     <see cref="ICacheStore" /> implementation backed by a Redis server.
/// </summary>
public sealed class RedisCacheService : ICacheStore, IHostedService, IDisposable
{
    private readonly string _configuration;
    private readonly ILogger<RedisCacheService> _log;
    private IDatabase _db = default!;
    private IConnectionMultiplexer _muxer = default!;

    public RedisCacheService(string configuration, ILogger<RedisCacheService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _log = logger;
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));

        try
        {
            return await _db.StringGetAsync(key);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Redis GET failed for {Key}", key);
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

        try
        {
            await _db.StringSetAsync(key, value, slidingExpiration);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Redis SET failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));

        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Redis DEL failed for {Key}", key);
        }
    }

    public void Dispose()
    {
        _muxer.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _muxer = await ConnectionMultiplexer.ConnectAsync(_configuration);
        _db = _muxer.GetDatabase();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}