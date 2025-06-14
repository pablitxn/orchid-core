using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Team.CreateTeam;

public class CreateTeamHandler(ITeamRepository repository, IAgentRepository agentRepository)
    : IRequestHandler<CreateTeamCommand, TeamEntity>
{
    private readonly IAgentRepository _agents = agentRepository;
    private readonly ITeamRepository _repository = repository;

    public async Task<TeamEntity> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = new TeamEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Policy = request.Policy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (request.Agents is not null)
            foreach (var a in request.Agents)
            {
                var agent = await _agents.GetByIdAsync(a.AgentId, cancellationToken);
                if (agent is null) continue;
                team.TeamAgents.Add(new TeamAgentEntity
                {
                    TeamId = team.Id,
                    AgentId = agent.Id,
                    Agent = agent,
                    Role = a.Role,
                    Order = a.Order
                });
            }

        await _repository.CreateAsync(team, cancellationToken);
        return team;
    }
}