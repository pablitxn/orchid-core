using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Agent.PermanentDeleteAgent;

public sealed class PermanentDeleteAgentHandler : IRequestHandler<PermanentDeleteAgentCommand, Unit>
{
    private readonly IAgentRepository _agentRepository;

    public PermanentDeleteAgentHandler(IAgentRepository agentRepository)
    {
        _agentRepository = agentRepository;
    }

    public async Task<Unit> Handle(PermanentDeleteAgentCommand command, CancellationToken cancellationToken)
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

        // Permanently delete from recycle bin (soft delete - mark as permanently deleted)
        agent.IsInRecycleBin = false;
        agent.UpdatedAt = DateTime.UtcNow;

        await _agentRepository.UpdateAsync(agent, cancellationToken);

        return Unit.Value;
    }
}