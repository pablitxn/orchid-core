namespace Domain.Entities;

/// <summary>
/// Represents a workspace/organization that groups users and resources together.
/// </summary>
public class WorkspaceEntity
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Unique slug for URL-friendly workspace identification.
    /// Example: "acme-corp" for "Acme Corporation"
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// The user who created and owns the workspace.
    /// </summary>
    public Guid OwnerId { get; set; }
    public UserEntity? Owner { get; set; }

    /// <summary>
    /// Whether the workspace is a personal workspace (auto-created for each user).
    /// Personal workspaces cannot be deleted or have members added.
    /// </summary>
    public bool IsPersonal { get; set; } = false;

    /// <summary>
    /// Soft delete timestamp.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public List<WorkspaceMemberEntity> Members { get; set; } = new();
}
