namespace Domain.Entities;

/// <summary>
/// Tracks moderation flags on plugin reviews.
/// Each user can flag a review once.
/// </summary>
public class PluginReviewFlagEntity
{
    public Guid Id { get; set; }

    public Guid ReviewId { get; set; }
    public virtual PluginReviewEntity? Review { get; set; }

    public Guid UserId { get; set; }
    public virtual UserEntity? User { get; set; }

    /// <summary>
    /// Reason for flagging (e.g., "spam", "offensive", "misleading")
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Optional additional context
    /// </summary>
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
