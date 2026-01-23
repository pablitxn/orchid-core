using Application.Interfaces;
using Domain.Exceptions;
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
            throw new AgentNotFoundException(command.AgentId);
        }

        if (agent.UserId != command.UserId)
        {
            throw new UnauthorizedDomainAccessException("Agent", command.AgentId, command.UserId);
        }

        if (!agent.IsInRecycleBin)
        {
            throw new AgentNotInRecycleBinException(command.AgentId);
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