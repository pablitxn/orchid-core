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
        IsUnlimited = entity.SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited
    };
}
