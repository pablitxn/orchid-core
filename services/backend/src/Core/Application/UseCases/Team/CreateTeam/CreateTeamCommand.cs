using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.Team.CreateTeam;

public record TeamAgentInput(Guid AgentId, string Role, int Order);

public class CreateTeamCommand(
    string name,
    string? description,
    TeamInteractionPolicy policy,
    IEnumerable<TeamAgentInput>? agents
) : IRequest<TeamEntity>
{
    public string Name { get; } = name;
    public string? Description { get; } = description;
    public TeamInteractionPolicy Policy { get; } = policy;
    public IEnumerable<TeamAgentInput>? Agents { get; } = agents;
}