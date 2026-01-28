using Domain.Enums;

namespace Domain.Entities;

public class UserEntity
{
    public Guid Id { get; init; }

    public string? Email { get; set; }

    public string? Name { get; set; }

    public string? AvatarUrl { get; set; }

    [Obsolete("Use SubscriptionEntity.Credits instead.")]
    public int? Credits { get; set; }

    [Obsolete("Use SubscriptionEntity to determine subscription activity instead.")]
    public bool SubscriptionActive { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public List<UserRoleEntity> UserRoles { get; set; } = new();

    // Optional hashed password for authentication
    public string PasswordHash { get; set; } = string.Empty;

    // Optional password reset token and expiration
    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetExpiry { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    /// <summary>
    /// Auth0 user identifier (sub claim). Null for legacy/local users.
    /// Example: "auth0|abc123" or "google-oauth2|123456789"
    /// </summary>
    public string? Auth0UserId { get; set; }

    /// <summary>
    /// Auth0 connection type. Helps identify the authentication method.
    /// Examples: "auth0", "google-oauth2", "github", "Username-Password-Authentication"
    /// </summary>
    public string? Auth0Connection { get; set; }

    /// <summary>
    /// Timestamp when Auth0 profile was last synced.
    /// </summary>
    public DateTime? Auth0LastSyncedAt { get; set; }

    /// <summary>
    /// Migration status for Auth0 Custom Database Connection lazy migration.
    /// </summary>
    public Auth0MigrationStatus Auth0MigrationStatus { get; set; } = Auth0MigrationStatus.NotMigrated;

    /// <summary>
    /// Timestamp when migration to Auth0 was completed.
    /// </summary>
    public DateTime? Auth0MigratedAt { get; set; }

    /// <summary>
    /// Error message if migration failed (for diagnostics).
    /// </summary>
    public string? Auth0MigrationError { get; set; }

    /// <summary>
    /// Default model preference for this user. Used when agent has no preference configured.
    /// </summary>
    public ModelPreference? ModelPreference { get; set; }

    /// <summary>
    /// Whether to allow automatic model degradation when preferred model is unavailable or too expensive.
    /// When true, the system will gracefully downgrade to a fallback/cheaper model.
    /// When false, operations will fail instead of degrading.
    /// </summary>
    public bool AllowAutoDegradation { get; set; } = true;

    // ─── MFA (Multi-Factor Authentication) ─────────────────────────────────────

    /// <summary>
    /// Whether MFA is enabled for this user in Auth0.
    /// </summary>
    public bool MfaEnabled { get; set; }

    /// <summary>
    /// Timestamp when MFA was first enrolled.
    /// </summary>
    public DateTime? MfaEnrolledAt { get; set; }

    /// <summary>
    /// Timestamp when MFA was last successfully verified.
    /// </summary>
    public DateTime? MfaLastVerifiedAt { get; set; }

    /// <summary>
    /// The MFA method used (e.g., "totp", "sms", "push").
    /// </summary>
    public string? MfaMethod { get; set; }
}