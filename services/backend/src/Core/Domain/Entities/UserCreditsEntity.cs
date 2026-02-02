using Domain.Exceptions;

namespace Domain.Entities;

/// <summary>
///     Represents the credit balance for a user, independent of subscriptions.
///     Users can have credits without an active subscription.
/// </summary>
public class UserCreditsEntity
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }
    public UserEntity? User { get; private set; }

    /// <summary>
    ///     Current available credits that can be consumed.
    /// </summary>
    public int AvailableCredits { get; set; }

    /// <summary>
    ///     Total credits ever granted to this user (for analytics).
    /// </summary>
    public int TotalCreditsGranted { get; set; }

    /// <summary>
    ///     Total credits ever consumed by this user (for analytics).
    /// </summary>
    public int TotalCreditsConsumed { get; set; }

    /// <summary>
    ///     Concurrency control for optimistic locking.
    /// </summary>
    public int Version { get; set; } = 0;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastConsumedAt { get; set; }

    /// <summary>
    ///     Grants credits to the user.
    /// </summary>
    public void GrantCredits(int amount)
    {
        if (amount <= 0)
            throw new ValidationException("Amount to grant must be positive.", nameof(amount));

        AvailableCredits += amount;
        TotalCreditsGranted += amount;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    ///     Consumes credits from the user's balance.
    /// </summary>
    public void ConsumeCredits(int amount)
    {
        if (amount <= 0)
            throw new ValidationException("Amount to consume must be positive.", nameof(amount));

        if (AvailableCredits < amount)
            throw new InsufficientCreditsException(AvailableCredits, amount);

        AvailableCredits -= amount;
        TotalCreditsConsumed += amount;
        LastConsumedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    ///     Refunds previously consumed credits back to the user.
    /// </summary>
    public void RefundCredits(int amount)
    {
        if (amount <= 0)
            throw new ValidationException("Refund amount must be positive.", nameof(amount));

        AvailableCredits += amount;
        if (TotalCreditsConsumed >= amount)
            TotalCreditsConsumed -= amount;

        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    ///     Checks if the user has sufficient credits for an operation.
    /// </summary>
    public bool HasSufficientCredits(int amount) => AvailableCredits >= amount;
}
