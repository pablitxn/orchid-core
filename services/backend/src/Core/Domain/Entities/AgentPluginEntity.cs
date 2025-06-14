namespace Domain.Entities;

public class AgentPluginEntity
{
    public Guid AgentId { get; set; }
    public AgentEntity Agent { get; set; } = null!;
    public Guid PluginId { get; set; }
    public PluginEntity Plugin { get; set; } = null!;
}