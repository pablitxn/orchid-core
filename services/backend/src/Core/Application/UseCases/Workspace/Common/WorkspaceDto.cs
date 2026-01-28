using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.Workspace.Common;

public sealed record WorkspaceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? AvatarUrl { get; init; }
    public string Slug { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }
    public bool IsPersonal { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int MemberCount { get; init; }

    /// <summary>
    /// Current user's role in this workspace (populated contextually).
    /// </summary>
    public WorkspaceRole? CurrentUserRole { get; init; }

    public static WorkspaceDto FromEntity(WorkspaceEntity entity, WorkspaceRole? currentUserRole = null) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        AvatarUrl = entity.AvatarUrl,
        Slug = entity.Slug,
        OwnerId = entity.OwnerId,
        IsPersonal = entity.IsPersonal,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        MemberCount = entity.Members?.Count(m => m.LeftAt == null) ?? 0,
        CurrentUserRole = currentUserRole
    };
}
