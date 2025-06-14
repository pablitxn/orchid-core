using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Agent.ListRecycleBinAgents;

public sealed class ListRecycleBinAgentsHandler : IRequestHandler<ListRecycleBinAgentsQuery, List<AgentEntity>>
{
    private readonly IAgentRepository _agentRepository;

    public ListRecycleBinAgentsHandler(IAgentRepository agentRepository)
    {
        _agentRepository = agentRepository;
    }

    public async Task<List<AgentEntity>> Handle(ListRecycleBinAgentsQuery request, CancellationToken cancellationToken)
    {
        return await _agentRepository.ListRecycleBinAsync(cancellationToken);
    }
}