using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

/// <summary>
/// Workspace-level subscription for shared billing.
/// Workspace subscriptions are separate from personal user subscriptions.
/// </summary>
public class WorkspaceSubscriptionEntity
{
    public Guid Id { get; init; }

    public Guid WorkspaceId { get; init; }
    public WorkspaceEntity? Workspace { get; private set; }

    public int Credits { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool AutoRenew { get; set; } = true;

    public Guid? SubscriptionPlanId { get; set; }
    public SubscriptionPlanEntity? SubscriptionPlan { get; set; }

    // Concurrency control
    public int Version { get; set; } = 0;

    // Stripe integration fields
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Incomplete;
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; } = false;
    public DateTime? CanceledAt { get; set; }
    public DateTime? TrialEndsAt { get; set; }

    /// <summary>
    /// Adds credits to the workspace subscription.
    /// </summary>
    public void AddCredits(int amount)
    {
        if (amount <= 0) throw new ValidationException("Amount to add must be positive.", nameof(amount));
        Credits += amount;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Consumes credits from the workspace subscription, ensuring sufficient balance.
    /// </summary>
    public void ConsumeCredits(int amount)
    {
        if (amount <= 0) throw new ValidationException("Amount to consume must be positive.", nameof(amount));

        if (SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited)
        {
            UpdatedAt = DateTime.UtcNow;
            Version++;
            return;
        }

        if (Credits < amount)
            throw new InsufficientCreditsException(Credits, amount);
        Credits -= amount;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Checks if the workspace subscription has unlimited credits.
    /// </summary>
    public bool HasUnlimitedCredits()
    {
        return SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited;
    }

    /// <summary>
    /// Checks if the workspace subscription is currently active.
    /// </summary>
    public bool IsActive()
    {
        return Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing;
    }

    /// <summary>
    /// Checks if the subscription can be reactivated (canceled but period not ended).
    /// </summary>
    public bool CanReactivate()
    {
        return CancelAtPeriodEnd && CurrentPeriodEnd.HasValue && CurrentPeriodEnd.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// Schedules the subscription for cancellation at period end.
    /// </summary>
    public void ScheduleCancellation()
    {
        if (!IsActive())
            throw new ValidationException("Cannot cancel an inactive subscription.", nameof(Status));

        CancelAtPeriodEnd = true;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Reactivates a subscription that was scheduled for cancellation.
    /// </summary>
    public void Reactivate()
    {
        if (!CanReactivate())
            throw new ValidationException("Subscription cannot be reactivated.", nameof(CancelAtPeriodEnd));

        CancelAtPeriodEnd = false;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Updates subscription from Stripe webhook data.
    /// </summary>
    public void UpdateFromStripe(
        SubscriptionStatus status,
        DateTime? periodStart,
        DateTime? periodEnd,
        bool cancelAtPeriodEnd,
        DateTime? canceledAt)
    {
        Status = status;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        CancelAtPeriodEnd = cancelAtPeriodEnd;
        CanceledAt = canceledAt;
        ExpiresAt = periodEnd;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Links the subscription to a Stripe customer and subscription.
    /// </summary>
    public void LinkToStripe(string customerId, string subscriptionId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ValidationException("Stripe customer ID is required.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(subscriptionId))
            throw new ValidationException("Stripe subscription ID is required.", nameof(subscriptionId));

        StripeCustomerId = customerId;
        StripeSubscriptionId = subscriptionId;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}
