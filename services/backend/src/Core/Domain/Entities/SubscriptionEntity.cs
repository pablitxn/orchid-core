using Domain.Enums;

namespace Domain.Entities;

public class SubscriptionEntity
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }
    public UserEntity? User { get; private set; }

    // Total available credits; immutable from outside except via AddCredits/ConsumeCredits
    public int Credits { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool AutoRenew { get; set; } = true;

    // Plan type to check if it's unlimited
    public Guid? SubscriptionPlanId { get; set; }
    public SubscriptionPlanEntity? SubscriptionPlan { get; set; }

    // Concurrency control
    public int Version { get; set; } = 0;


    /// <summary>
    ///     Adds credits to the subscription.
    /// </summary>
    public void AddCredits(int amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount to add must be positive.", nameof(amount));
        Credits += amount;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    ///     Consumes credits from the subscription, ensuring sufficient balance.
    /// </summary>
    public void ConsumeCredits(int amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount to consume must be positive.", nameof(amount));
        
        // Check if subscription has unlimited plan
        if (SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited)
        {
            // Unlimited plan - no credit deduction needed
            UpdatedAt = DateTime.UtcNow;
            Version++;
            return;
        }
        
        if (Credits < amount)
            throw new InvalidOperationException($"Insufficient credits: available {Credits}, requested {amount}.");
        Credits -= amount;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    ///     Checks if the subscription has unlimited credits.
    /// </summary>
    public bool HasUnlimitedCredits()
    {
        return SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited;
    }
}