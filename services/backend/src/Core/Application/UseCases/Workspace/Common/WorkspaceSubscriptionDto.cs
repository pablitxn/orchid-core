using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.Workspace.Common;

public sealed record WorkspaceSubscriptionDto
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public int Credits { get; init; }
    public bool IsUnlimited { get; init; }
    public SubscriptionStatus Status { get; init; }
    public string? PlanName { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? CurrentPeriodEnd { get; init; }
    public bool CancelAtPeriodEnd { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static WorkspaceSubscriptionDto FromEntity(WorkspaceSubscriptionEntity entity) => new()
    {
        Id = entity.Id,
        WorkspaceId = entity.WorkspaceId,
        Credits = entity.Credits,
        IsUnlimited = entity.HasUnlimitedCredits(),
        Status = entity.Status,
        PlanName = entity.SubscriptionPlan?.Name,
        ExpiresAt = entity.ExpiresAt,
        CurrentPeriodEnd = entity.CurrentPeriodEnd,
        CancelAtPeriodEnd = entity.CancelAtPeriodEnd,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
