namespace Domain.Entities;

/// <summary>
/// Tracks helpful votes on plugin reviews.
/// Each user can vote helpful once per review (toggle behavior).
/// </summary>
public class PluginReviewHelpfulVoteEntity
{
    public Guid Id { get; set; }

    public Guid ReviewId { get; set; }
    public virtual PluginReviewEntity? Review { get; set; }

    public Guid UserId { get; set; }
    public virtual UserEntity? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
