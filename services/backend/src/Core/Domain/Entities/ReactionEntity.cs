namespace Domain.Entities;

/// <summary>
/// Represents a user reaction (emoji) on a message or artifact.
/// </summary>
public class ReactionEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Target type discriminator: 'message' or 'artifact'.
    /// </summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the target (MessageV2Entity.Id or MessageArtifactEntity.Id).
    /// </summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// The user who reacted.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public UserEntity? User { get; set; }

    /// <summary>
    /// Emoji character(s) or URL for custom emoji.
    /// </summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>
    /// Reaction type: 'emoji' or 'custom'.
    /// </summary>
    public string Type { get; set; } = "emoji";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
