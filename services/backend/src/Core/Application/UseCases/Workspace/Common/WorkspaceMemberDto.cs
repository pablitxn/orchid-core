using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.Workspace.Common;

public sealed record WorkspaceMemberDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? UserName { get; init; }
    public string? UserEmail { get; init; }
    public string? UserAvatarUrl { get; init; }
    public WorkspaceRole Role { get; init; }
    public DateTime JoinedAt { get; init; }
    public Guid? InvitedById { get; init; }
    public string? InvitedByName { get; init; }

    public static WorkspaceMemberDto FromEntity(WorkspaceMemberEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        UserName = entity.User?.Name,
        UserEmail = entity.User?.Email,
        UserAvatarUrl = entity.User?.AvatarUrl,
        Role = entity.Role,
        JoinedAt = entity.JoinedAt,
        InvitedById = entity.InvitedById,
        InvitedByName = entity.InvitedBy?.Name
    };
}
