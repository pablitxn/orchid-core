namespace Application.Interfaces;

/// <summary>
/// Service for managing free tier daily credit allocations.
/// </summary>
public interface IFreeTierCreditService
{
    /// <summary>
    /// Checks if a user is eligible for free tier credits (no active subscription).
    /// </summary>
    Task<bool> IsEligibleForFreeTierAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last date when free tier credits were granted to the user.
    /// </summary>
    Task<DateTime?> GetLastResetDateAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants daily free tier credits to an eligible user.
    /// </summary>
    Task GrantDailyCreditsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of free tier credits remaining for today.
    /// </summary>
    Task<int> GetRemainingDailyCreditsAsync(Guid userId, CancellationToken cancellationToken = default);
}
