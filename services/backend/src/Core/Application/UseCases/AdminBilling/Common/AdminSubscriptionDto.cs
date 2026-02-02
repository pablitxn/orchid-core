using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.AdminBilling.Common;

/// <summary>
/// DTO for admin subscription view with additional user information.
/// </summary>
public sealed record AdminSubscriptionDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? UserEmail { get; init; }
    public string? UserName { get; init; }
    public int Credits { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool AutoRenew { get; init; }
    public Guid? SubscriptionPlanId { get; init; }
    public string? PlanName { get; init; }
    public bool IsUnlimited { get; init; }
    public SubscriptionStatus Status { get; init; }
    public DateTime? CurrentPeriodStart { get; init; }
    public DateTime? CurrentPeriodEnd { get; init; }
    public bool CancelAtPeriodEnd { get; init; }
    public DateTime? CanceledAt { get; init; }
    public bool IsActive { get; init; }
    public string? StripeCustomerId { get; init; }
    public string? StripeSubscriptionId { get; init; }

    public static AdminSubscriptionDto FromEntity(SubscriptionEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        UserEmail = entity.User?.Email,
        UserName = entity.User?.Name,
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
        StripeCustomerId = entity.StripeCustomerId,
        StripeSubscriptionId = entity.StripeSubscriptionId
    };

    public static AdminSubscriptionDto FromEntityWithCredits(SubscriptionEntity? entity, UserCreditsEntity userCredits)
    {
        if (entity == null)
        {
            return new AdminSubscriptionDto
            {
                Id = Guid.Empty,
                UserId = userCredits.UserId,
                UserEmail = null,
                UserName = null,
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
                StripeCustomerId = null,
                StripeSubscriptionId = null
            };
        }

        return new AdminSubscriptionDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            UserEmail = entity.User?.Email,
            UserName = entity.User?.Name,
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
            StripeCustomerId = entity.StripeCustomerId,
            StripeSubscriptionId = entity.StripeSubscriptionId
        };
    }
}
