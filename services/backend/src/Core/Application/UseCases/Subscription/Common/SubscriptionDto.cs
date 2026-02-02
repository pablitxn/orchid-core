using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.Subscription.Common;

public sealed record SubscriptionDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public int Credits { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool AutoRenew { get; init; }
    public Guid? SubscriptionPlanId { get; init; }
    public string? PlanName { get; init; }
    public bool IsUnlimited { get; init; }

    // Stripe integration fields
    public SubscriptionStatus Status { get; init; }
    public DateTime? CurrentPeriodStart { get; init; }
    public DateTime? CurrentPeriodEnd { get; init; }
    public bool CancelAtPeriodEnd { get; init; }
    public DateTime? CanceledAt { get; init; }
    public bool IsActive { get; init; }
    public bool CanReactivate { get; init; }

    public static SubscriptionDto FromEntity(SubscriptionEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        Credits = entity.Credits,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        ExpiresAt = entity.ExpiresAt,
        AutoRenew = entity.AutoRenew,
        SubscriptionPlanId = entity.SubscriptionPlanId,
        PlanName = entity.SubscriptionPlan?.Name,
        IsUnlimited = entity.SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited,
        Status = entity.Status,
        CurrentPeriodStart = entity.CurrentPeriodStart,
        CurrentPeriodEnd = entity.CurrentPeriodEnd,
        CancelAtPeriodEnd = entity.CancelAtPeriodEnd,
        CanceledAt = entity.CanceledAt,
        IsActive = entity.IsActive(),
        CanReactivate = entity.CanReactivate()
    };

    /// <summary>
    ///     Creates a DTO from subscription entity with credits from UserCreditsEntity.
    /// </summary>
    public static SubscriptionDto FromEntityWithCredits(SubscriptionEntity? entity, UserCreditsEntity userCredits)
    {
        if (entity == null)
        {
            // No subscription, but user has credits
            return new SubscriptionDto
            {
                Id = Guid.Empty,
                UserId = userCredits.UserId,
                Credits = userCredits.AvailableCredits,
                CreatedAt = userCredits.CreatedAt,
                UpdatedAt = userCredits.UpdatedAt,
                ExpiresAt = null,
                AutoRenew = false,
                SubscriptionPlanId = null,
                PlanName = null,
                IsUnlimited = false,
                Status = SubscriptionStatus.Incomplete,
                CurrentPeriodStart = null,
                CurrentPeriodEnd = null,
                CancelAtPeriodEnd = false,
                CanceledAt = null,
                IsActive = false,
                CanReactivate = false
            };
        }

        return new SubscriptionDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Credits = userCredits.AvailableCredits,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ExpiresAt = entity.ExpiresAt,
            AutoRenew = entity.AutoRenew,
            SubscriptionPlanId = entity.SubscriptionPlanId,
            PlanName = entity.SubscriptionPlan?.Name,
            IsUnlimited = entity.SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited,
            Status = entity.Status,
            CurrentPeriodStart = entity.CurrentPeriodStart,
            CurrentPeriodEnd = entity.CurrentPeriodEnd,
            CancelAtPeriodEnd = entity.CancelAtPeriodEnd,
            CanceledAt = entity.CanceledAt,
            IsActive = entity.IsActive(),
            CanReactivate = entity.CanReactivate()
        };
    }
}
