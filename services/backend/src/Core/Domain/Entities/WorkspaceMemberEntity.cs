using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Represents a user's membership in a workspace with their assigned role.
/// </summary>
public class WorkspaceMemberEntity
{
    public Guid Id { get; init; }

    public Guid WorkspaceId { get; set; }
    public WorkspaceEntity Workspace { get; set; } = null!;

    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = null!;

    /// <summary>
    /// Role within the workspace: Owner, Admin, or Member.
    /// </summary>
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Member;

    /// <summary>
    /// User who invited this member (null if original owner/creator).
    /// </summary>
    public Guid? InvitedById { get; set; }
    public UserEntity? InvitedBy { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the member left the workspace (soft removal).
    /// </summary>
    public DateTime? LeftAt { get; set; }
}
