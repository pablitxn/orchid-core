using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.Team.Common;

public sealed record TeamAgentDto
{
    public Guid AgentId { get; init; }
    public string Role { get; init; } = string.Empty;
    public int Order { get; init; }

    public static TeamAgentDto FromEntity(TeamAgentEntity entity) => new()
    {
        AgentId = entity.AgentId,
        Role = entity.Role,
        Order = entity.Order
    };
}

public sealed record TeamDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public TeamInteractionPolicy Policy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<TeamAgentDto> TeamAgents { get; init; } = new();

    public static TeamDto FromEntity(TeamEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        Policy = entity.Policy,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        TeamAgents = entity.TeamAgents.Select(TeamAgentDto.FromEntity).ToList()
    };
}
