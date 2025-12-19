using System.Collections.Concurrent;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// In-memory rate limiting service for development or when Redis is not available.
/// Uses a sliding window approach with cleanup of expired entries.
/// </summary>
public class InMemoryRateLimitService : IRateLimitService
{
    private readonly ConcurrentDictionary<string, SortedSet<long>> _requests = new();
    private readonly ILogger<InMemoryRateLimitService> _logger;
    private readonly object _lockObject = new();

    public InMemoryRateLimitService(ILogger<InMemoryRateLimitService> logger)
    {
        _logger = logger;
    }

    public Task<RateLimitResult> CheckAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;

        var requests = _requests.GetOrAdd(key, _ => new SortedSet<long>());

        lock (_lockObject)
        {
            // Remove old entries outside the window
            requests.RemoveWhere(timestamp => timestamp < windowStart);

            var currentCount = requests.Count;

            if (currentCount >= limit)
            {
                var oldestEntry = requests.Min;
                var resetTime = DateTimeOffset.FromUnixTimeMilliseconds(oldestEntry + (long)window.TotalMilliseconds).UtcDateTime;

                _logger.LogWarning("Rate limit exceeded for key: {Key}. Current: {Current}, Limit: {Limit}",
                    key, currentCount, limit);

                return Task.FromResult(RateLimitResult.Exceeded(currentCount, limit, resetTime));
            }

            // Add the current request
            requests.Add(now);

            var resetTimeForAllowed = DateTime.UtcNow.Add(window);
            return Task.FromResult(RateLimitResult.Allowed(currentCount + 1, limit, resetTimeForAllowed));
        }
    }

    public Task RecordRequestAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;

        var requests = _requests.GetOrAdd(key, _ => new SortedSet<long>());

        lock (_lockObject)
        {
            requests.Add(now);
            requests.RemoveWhere(timestamp => timestamp < windowStart);
        }

        return Task.CompletedTask;
    }
}
