namespace Domain.Entities;

public class TeamAgentEntity
{
    public Guid TeamId { get; set; }
    public TeamEntity Team { get; set; } = null!;
    public Guid AgentId { get; set; }
    public AgentEntity Agent { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public int Order { get; set; }
}