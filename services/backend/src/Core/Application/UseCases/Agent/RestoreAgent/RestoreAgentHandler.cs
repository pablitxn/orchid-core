using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Agent.RestoreAgent;

public sealed class RestoreAgentHandler : IRequestHandler<RestoreAgentCommand, Unit>
{
    private readonly IAgentRepository _agentRepository;

    public RestoreAgentHandler(IAgentRepository agentRepository)
    {
        _agentRepository = agentRepository;
    }

    public async Task<Unit> Handle(RestoreAgentCommand command, CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.GetByIdAsync(command.AgentId, cancellationToken);
        
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with ID {command.AgentId} not found.");
        }

        if (!agent.IsInRecycleBin)
        {
            throw new InvalidOperationException($"Agent with ID {command.AgentId} is not in recycle bin.");
        }

        // Restore from recycle bin
        agent.IsDeleted = false;
        agent.IsInRecycleBin = false;
        agent.DeletedAt = null;
        agent.RecycleBinExpiresAt = null;
        agent.UpdatedAt = DateTime.UtcNow;

        await _agentRepository.UpdateAsync(agent, cancellationToken);

        return Unit.Value;
    }
}