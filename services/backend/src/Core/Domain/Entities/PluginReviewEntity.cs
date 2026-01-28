namespace Domain.Entities;

/// <summary>
/// Represents a user's review for a plugin.
/// Each user can review a plugin once, but can update their review.
/// </summary>
public class PluginReviewEntity
{
    public Guid Id { get; set; }

    public Guid PluginId { get; set; }
    public virtual PluginEntity? Plugin { get; set; }

    public Guid UserId { get; set; }
    public virtual UserEntity? User { get; set; }

    /// <summary>
    /// Rating value from 1 to 5
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Review title (max 100 chars)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Review body text (max 2000 chars)
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Number of helpful votes
    /// </summary>
    public int HelpfulCount { get; set; } = 0;

    /// <summary>
    /// Whether user is verified (owned plugin 24h+ before review)
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
