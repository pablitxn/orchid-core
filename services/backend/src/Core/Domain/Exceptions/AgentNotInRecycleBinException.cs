namespace Domain.Exceptions;

public class AgentNotInRecycleBinException : Exception
{
    public AgentNotInRecycleBinException(Guid agentId)
        : base($"Agent '{agentId}' is not in the recycle bin.")
    {
        AgentId = agentId;
    }

    public Guid AgentId { get; }
}
