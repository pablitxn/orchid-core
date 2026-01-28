using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.ChatSession.Common;

public sealed record ChatSessionDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid? AgentId { get; init; }
    public Guid? TeamId { get; init; }
    public InteractionType InteractionType { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public string? Title { get; init; }
    public bool IsArchived { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? ModelOverride { get; init; }
    public DateTime? ModelChangedAt { get; init; }

    public static ChatSessionDto FromEntity(ChatSessionEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        AgentId = entity.AgentId,
        TeamId = entity.TeamId,
        InteractionType = entity.InteractionType,
        SessionId = entity.SessionId,
        Title = entity.Title,
        IsArchived = entity.IsArchived,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        ModelOverride = entity.ModelOverride,
        ModelChangedAt = entity.ModelChangedAt
    };
}
