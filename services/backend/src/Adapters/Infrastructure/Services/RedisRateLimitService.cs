using Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Services;

public class RedisRateLimitService : IRateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimitService> _logger;
    private const string RateLimitPrefix = "rate_limit:";

    public RedisRateLimitService(IConnectionMultiplexer redis, ILogger<RedisRateLimitService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<RateLimitResult> CheckAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var rateLimitKey = $"{RateLimitPrefix}{key}";
        
        // Use a sliding window approach with Redis sorted sets
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;
        
        // Remove old entries outside the window
        await db.SortedSetRemoveRangeByScoreAsync(rateLimitKey, 0, windowStart);
        
        // Count current requests in the window
        var currentCount = await db.SortedSetLengthAsync(rateLimitKey);
        
        if (currentCount >= limit)
        {
            // Get the oldest entry to determine when the limit resets
            var oldestEntry = await db.SortedSetRangeByScoreWithScoresAsync(rateLimitKey, take: 1);
            var resetTime = oldestEntry.Length > 0 
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)oldestEntry[0].Score + (long)window.TotalMilliseconds).UtcDateTime
                : DateTime.UtcNow.Add(window);
            
            _logger.LogWarning("Rate limit exceeded for key: {Key}. Current: {Current}, Limit: {Limit}", 
                key, currentCount, limit);
                
            return RateLimitResult.Exceeded((int)currentCount, limit, resetTime);
        }
        
        // Add the current request
        await db.SortedSetAddAsync(rateLimitKey, Guid.NewGuid().ToString(), now);
        
        // Set expiration on the key
        await db.KeyExpireAsync(rateLimitKey, window);
        
        var resetTimeForAllowed = DateTime.UtcNow.Add(window);
        return RateLimitResult.Allowed((int)currentCount + 1, limit, resetTimeForAllowed);
    }

    public async Task RecordRequestAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var rateLimitKey = $"{RateLimitPrefix}{key}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        // Add the request
        await db.SortedSetAddAsync(rateLimitKey, Guid.NewGuid().ToString(), now);
        
        // Set expiration
        await db.KeyExpireAsync(rateLimitKey, window);
        
        // Clean up old entries
        var windowStart = now - (long)window.TotalMilliseconds;
        await db.SortedSetRemoveRangeByScoreAsync(rateLimitKey, 0, windowStart);
    }
}

/// <summary>
/// Alternative implementation using a fixed window counter approach (simpler but less accurate)
/// </summary>
public class RedisFixedWindowRateLimitService : IRateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisFixedWindowRateLimitService> _logger;
    private const string RateLimitPrefix = "rate_limit_fixed:";

    public RedisFixedWindowRateLimitService(IConnectionMultiplexer redis, ILogger<RedisFixedWindowRateLimitService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<RateLimitResult> CheckAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        // Create a key based on the current time window
        var windowKey = GetWindowKey(key, window);
        
        // Increment the counter atomically
        var currentCount = await db.StringIncrementAsync(windowKey);
        
        // Set expiration if this is the first request in the window
        if (currentCount == 1)
        {
            await db.KeyExpireAsync(windowKey, window);
        }
        
        var resetTime = GetNextWindowStart(window);
        
        if (currentCount > limit)
        {
            _logger.LogWarning("Rate limit exceeded for key: {Key}. Current: {Current}, Limit: {Limit}", 
                key, currentCount, limit);
                
            return RateLimitResult.Exceeded((int)currentCount, limit, resetTime);
        }
        
        return RateLimitResult.Allowed((int)currentCount, limit, resetTime);
    }

    public async Task RecordRequestAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var windowKey = GetWindowKey(key, window);
        
        var count = await db.StringIncrementAsync(windowKey);
        
        if (count == 1)
        {
            await db.KeyExpireAsync(windowKey, window);
        }
    }

    private string GetWindowKey(string key, TimeSpan window)
    {
        var windowStart = GetCurrentWindowStart(window);
        return $"{RateLimitPrefix}{key}:{windowStart.Ticks}";
    }

    private DateTime GetCurrentWindowStart(TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var windowTicks = window.Ticks;
        var currentWindowNumber = now.Ticks / windowTicks;
        return new DateTime(currentWindowNumber * windowTicks, DateTimeKind.Utc);
    }

    private DateTime GetNextWindowStart(TimeSpan window)
    {
        return GetCurrentWindowStart(window).Add(window);
    }
}