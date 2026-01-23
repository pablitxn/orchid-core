namespace Domain.Exceptions;

public class AgentNotFoundException : EntityNotFoundException
{
    public AgentNotFoundException(Guid agentId)
        : base("Agent", agentId)
    {
        AgentId = agentId;
    }

    public Guid AgentId { get; }
}
