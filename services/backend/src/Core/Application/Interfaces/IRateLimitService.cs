namespace Application.Interfaces;

/// <summary>
/// Service for implementing rate limiting functionality
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks if a request is allowed based on rate limiting rules
    /// </summary>
    /// <param name="key">The rate limit key (e.g., user ID, IP address)</param>
    /// <param name="limit">Maximum number of requests allowed</param>
    /// <param name="window">Time window for the rate limit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the request is allowed, false if rate limit exceeded</returns>
    Task<RateLimitResult> CheckAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Records a request for rate limiting purposes
    /// </summary>
    /// <param name="key">The rate limit key</param>
    /// <param name="window">Time window for the rate limit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordRequestAsync(string key, TimeSpan window, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a rate limit check
/// </summary>
public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public int CurrentCount { get; set; }
    public int Limit { get; set; }
    public DateTime ResetTime { get; set; }
    public TimeSpan RetryAfter { get; set; }
    
    public static RateLimitResult Allowed(int currentCount, int limit, DateTime resetTime) => new()
    {
        IsAllowed = true,
        CurrentCount = currentCount,
        Limit = limit,
        ResetTime = resetTime
    };
    
    public static RateLimitResult Exceeded(int currentCount, int limit, DateTime resetTime) => new()
    {
        IsAllowed = false,
        CurrentCount = currentCount,
        Limit = limit,
        ResetTime = resetTime,
        RetryAfter = resetTime - DateTime.UtcNow
    };
}