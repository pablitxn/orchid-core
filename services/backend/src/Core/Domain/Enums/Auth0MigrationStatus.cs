namespace Domain.Enums;

/// <summary>
/// Tracks the Auth0 migration status for a user.
/// </summary>
public enum Auth0MigrationStatus
{
    /// <summary>
    /// User has not been migrated to Auth0 (legacy auth only).
    /// </summary>
    NotMigrated,

    /// <summary>
    /// User successfully migrated to Auth0 (password now in Auth0).
    /// </summary>
    Completed,

    /// <summary>
    /// Migration failed; user should use password reset.
    /// </summary>
    Failed
}
