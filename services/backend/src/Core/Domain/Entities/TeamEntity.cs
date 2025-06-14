using Domain.Enums;

namespace Domain.Entities;

public class TeamEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public TeamInteractionPolicy Policy { get; set; } = TeamInteractionPolicy.Open;
    public List<TeamAgentEntity> TeamAgents { get; set; } = new();
}