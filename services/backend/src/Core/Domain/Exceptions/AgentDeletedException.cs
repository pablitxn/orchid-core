namespace Domain.Exceptions;

public class AgentDeletedException : Exception
{
    public AgentDeletedException(Guid agentId)
        : base($"Agent '{agentId}' is deleted and cannot be modified.")
    {
        AgentId = agentId;
    }

    public Guid AgentId { get; }
}
