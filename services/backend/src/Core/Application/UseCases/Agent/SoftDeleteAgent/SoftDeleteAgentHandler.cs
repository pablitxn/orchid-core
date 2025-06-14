using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Agent.SoftDeleteAgent;

public sealed class SoftDeleteAgentHandler : IRequestHandler<SoftDeleteAgentCommand, Unit>
{
    private readonly IAgentRepository _agentRepository;

    public SoftDeleteAgentHandler(IAgentRepository agentRepository)
    {
        _agentRepository = agentRepository;
    }

    public async Task<Unit> Handle(SoftDeleteAgentCommand command, CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.GetByIdAsync(command.AgentId, cancellationToken);
        
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with ID {command.AgentId} not found.");
        }

        // Check if user owns the agent
        if (agent.UserId != command.UserId)
        {
            throw new UnauthorizedAccessException($"User {command.UserId} is not authorized to delete agent {command.AgentId}.");
        }

        // Move to recycle bin with 30-day expiration
        agent.IsDeleted = true;
        agent.IsInRecycleBin = true;
        agent.DeletedAt = DateTime.UtcNow;
        agent.RecycleBinExpiresAt = DateTime.UtcNow.AddDays(30);
        agent.UpdatedAt = DateTime.UtcNow;

        await _agentRepository.UpdateAsync(agent, cancellationToken);

        return Unit.Value;
    }
}