using System.Text.Json.Serialization;
using Application.UseCases.Team.CreateTeam;
using Domain.Enums;

namespace WebApi.Models;

public record CreateTeamRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required TeamInteractionPolicy Policy { get; init; }
    
    public IEnumerable<TeamAgentInput>? Agents { get; init; }

    public CreateTeamCommand ToCommand()
    {
        return new CreateTeamCommand(Name, Description, Policy, Agents);
    }
}