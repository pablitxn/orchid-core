using Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Redis-based implementation of JWT token blacklist for immediate token invalidation.
/// Tokens are stored until their expiration time to prevent reuse after logout.
/// Uses IDistributedCache for compatibility with the existing Redis configuration.
/// </summary>
public class RedisJwtBlacklistService : IJwtBlacklistService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisJwtBlacklistService> _logger;
    private const string BlacklistPrefix = "jwt_blacklist:";

    public RedisJwtBlacklistService(IDistributedCache cache, ILogger<RedisJwtBlacklistService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task BlacklistTokenAsync(string jti, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jti))
        {
            _logger.LogWarning("Attempted to blacklist token with null or empty JTI");
            return;
        }

        var key = $"{BlacklistPrefix}{jti}";

        // Calculate the TTL based on token expiration
        var ttl = expiresAt - DateTime.UtcNow;

        // Only blacklist if the token hasn't already expired
        if (ttl > TimeSpan.Zero)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiresAt
            };

            await _cache.SetStringAsync(key, "1", options, cancellationToken);
            _logger.LogInformation("Token {Jti} blacklisted until {ExpiresAt}", jti, expiresAt);
        }
        else
        {
            _logger.LogDebug("Token {Jti} already expired, skipping blacklist", jti);
        }
    }

    public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jti))
        {
            return false;
        }

        var key = $"{BlacklistPrefix}{jti}";

        var value = await _cache.GetStringAsync(key, cancellationToken);
        var exists = value != null;

        if (exists)
        {
            _logger.LogDebug("Token {Jti} found in blacklist", jti);
        }

        return exists;
    }
}
