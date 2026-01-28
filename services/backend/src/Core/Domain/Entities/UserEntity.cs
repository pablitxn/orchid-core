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
}