namespace Domain.Enums;

/// <summary>
/// Represents the role of a user within a workspace.
/// </summary>
public enum WorkspaceRole
{
    /// <summary>
    /// Full control: billing, delete workspace, manage all members.
    /// </summary>
    Owner = 0,

    /// <summary>
    /// Manage members, resources, and settings (except billing/deletion).
    /// </summary>
    Admin = 1,

    /// <summary>
    /// Access and use workspace resources.
    /// </summary>
    Member = 2
}
