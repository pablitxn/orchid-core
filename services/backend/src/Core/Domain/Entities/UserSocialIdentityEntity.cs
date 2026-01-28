namespace Domain.Entities;

/// <summary>
/// Represents a social identity linked to a user account.
/// Users can have multiple social identities (Google, GitHub, etc.) linked to their account.
/// </summary>
public class UserSocialIdentityEntity
{
    /// <summary>
    /// Unique identifier for this social identity record.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The user this social identity belongs to.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The social provider name (e.g., "google-oauth2", "github").
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The user's ID as assigned by the social provider.
    /// </summary>
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>
    /// Email address from the social provider (may differ from primary email).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Display name from the social provider.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Avatar URL from the social provider.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Whether this is the primary identity used for authentication.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// When this social identity was first linked.
    /// </summary>
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this social identity was last used for login.
    /// </summary>
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this social identity was unlinked (soft delete).
    /// Null if still linked.
    /// </summary>
    public DateTime? UnlinkedAt { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public UserEntity User { get; set; } = null!;
}
