using Application.Interfaces;
using Domain.Exceptions;
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
            throw new AgentNotFoundException(command.AgentId);
        }

        // Check if user owns the agent
        if (agent.UserId != command.UserId)
        {
            throw new UnauthorizedDomainAccessException("Agent", command.AgentId, command.UserId);
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