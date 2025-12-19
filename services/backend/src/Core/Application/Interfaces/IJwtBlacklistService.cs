namespace Application.Interfaces;

/// <summary>
/// Service for managing JWT token blacklist to enable immediate token invalidation on logout.
/// </summary>
public interface IJwtBlacklistService
{
    /// <summary>
    /// Adds a JWT token to the blacklist until its expiration time.
    /// </summary>
    /// <param name="jti">The unique token identifier (jti claim).</param>
    /// <param name="expiresAt">The token's expiration time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BlacklistTokenAsync(string jti, DateTime expiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a JWT token is blacklisted.
    /// </summary>
    /// <param name="jti">The unique token identifier (jti claim).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the token is blacklisted, false otherwise.</returns>
    Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default);
}
