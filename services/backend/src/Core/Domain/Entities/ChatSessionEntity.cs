using Domain.Enums;

namespace Domain.Entities;

public class ChatSessionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AgentId { get; set; }
    public AgentEntity? Agent { get; set; }
    public Guid? TeamId { get; set; }
    public TeamEntity? Team { get; set; }
    public InteractionType InteractionType { get; set; } = InteractionType.Text;
    public string SessionId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Model override for this session. When set, takes highest priority in model resolution.
    /// </summary>
    public string? ModelOverride { get; set; }

    /// <summary>
    /// Timestamp when the model was last changed for this session.
    /// </summary>
    public DateTime? ModelChangedAt { get; set; }
}