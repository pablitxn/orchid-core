using Application.Interfaces;
using Domain.Exceptions;
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

        // Permanently delete from database
        await _agentRepository.DeleteAsync(command.AgentId, cancellationToken);

        return Unit.Value;
    }
}