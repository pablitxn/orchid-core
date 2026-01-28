using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.Workspace.Common;

public sealed record WorkspaceDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? AvatarUrl { get; init; }
    public string Slug { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public bool IsPersonal { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public WorkspaceRole CurrentUserRole { get; init; }
    public List<WorkspaceMemberDto> Members { get; init; } = new();
    public WorkspaceSubscriptionDto? Subscription { get; init; }

    public static WorkspaceDetailDto FromEntity(
        WorkspaceEntity entity,
        WorkspaceRole currentUserRole,
        WorkspaceSubscriptionEntity? subscription = null) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        AvatarUrl = entity.AvatarUrl,
        Slug = entity.Slug,
        OwnerId = entity.OwnerId,
        OwnerName = entity.Owner?.Name,
        IsPersonal = entity.IsPersonal,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        CurrentUserRole = currentUserRole,
        Members = entity.Members?
            .Where(m => m.LeftAt == null)
            .Select(WorkspaceMemberDto.FromEntity)
            .ToList() ?? new(),
        Subscription = subscription != null ? WorkspaceSubscriptionDto.FromEntity(subscription) : null
    };
}
