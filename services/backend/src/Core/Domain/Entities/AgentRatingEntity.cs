using Core.Domain.Entities;

namespace Domain.Entities;

/// <summary>
/// Represents a user's rating for an agent.
/// Each user can rate an agent once, but can update their rating.
/// </summary>
public class AgentRatingEntity
{
    public Guid Id { get; set; }

    public Guid AgentId { get; set; }
    public virtual AgentEntity? Agent { get; set; }

    public Guid UserId { get; set; }
    public virtual UserEntity? User { get; set; }

    /// <summary>
    /// Rating value from 1 to 5
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Optional comment/review from the user
    /// </summary>
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
