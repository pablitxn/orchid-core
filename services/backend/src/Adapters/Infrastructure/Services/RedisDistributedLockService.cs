using System.Diagnostics;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Services;

public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDistributedLockService> _logger;
    private const string LockPrefix = "lock:";

    public RedisDistributedLockService(IConnectionMultiplexer redis, ILogger<RedisDistributedLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IDistributedLock?> AcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var lockKey = $"{LockPrefix}{key}";
        var lockValue = Guid.NewGuid().ToString();
        var db = _redis.GetDatabase();

        var acquired = await db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);
        
        if (acquired)
        {
            _logger.LogDebug("Acquired distributed lock for key: {Key}", key);
            return new RedisDistributedLock(db, lockKey, lockValue, _logger);
        }

        return null;
    }

    public async Task<IDistributedLock> WaitAsync(string key, TimeSpan expiry, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var retryDelay = TimeSpan.FromMilliseconds(50);
        
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var lockHandle = await AcquireAsync(key, expiry, cancellationToken);
            if (lockHandle != null)
            {
                return lockHandle;
            }

            await Task.Delay(retryDelay, cancellationToken);
            
            // Exponential backoff with jitter
            retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 1.5 + Random.Shared.Next(0, 100), 1000));
        }

        throw new TimeoutException($"Could not acquire distributed lock for key '{key}' within {timeout}");
    }

    private class RedisDistributedLock : IDistributedLock
    {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _value;
        private readonly ILogger _logger;
        private bool _disposed;

        public RedisDistributedLock(IDatabase db, string key, string value, ILogger logger)
        {
            _db = db;
            _key = key;
            _value = value;
            _logger = logger;
            IsHeld = true;
        }

        public string Key => _key;
        public bool IsHeld { get; private set; }

        public async Task ExtendAsync(TimeSpan extension, CancellationToken cancellationToken = default)
        {
            if (!IsHeld || _disposed)
                throw new InvalidOperationException("Lock is no longer held");

            // Use Lua script to atomically check and extend
            const string script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('pexpire', KEYS[1], ARGV[2])
                else
                    return 0
                end";

            var result = await _db.ScriptEvaluateAsync(
                script, 
                new RedisKey[] { _key }, 
                new RedisValue[] { _value, (long)extension.TotalMilliseconds });

            if ((long)result == 0)
            {
                IsHeld = false;
                throw new InvalidOperationException("Lock was lost or expired");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (!IsHeld)
                return;

            try
            {
                // Use Lua script to atomically check and delete
                const string script = @"
                    if redis.call('get', KEYS[1]) == ARGV[1] then
                        return redis.call('del', KEYS[1])
                    else
                        return 0
                    end";

                await _db.ScriptEvaluateAsync(
                    script, 
                    new RedisKey[] { _key }, 
                    new RedisValue[] { _value });

                IsHeld = false;
                _logger.LogDebug("Released distributed lock for key: {Key}", _key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing distributed lock for key: {Key}", _key);
            }
        }
    }
}