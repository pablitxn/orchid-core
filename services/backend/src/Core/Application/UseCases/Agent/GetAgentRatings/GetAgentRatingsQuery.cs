using Application.UseCases.Agent.RateAgent;
using MediatR;

namespace Application.UseCases.Agent.GetAgentRatings;

/// <summary>
/// Query to get all ratings for an agent, including average rating and total count
/// </summary>
public class GetAgentRatingsQuery(Guid agentId) : IRequest<AgentRatingsResultDto>
{
    public Guid AgentId { get; set; } = agentId;
}

/// <summary>
/// Result containing all ratings and summary statistics
/// </summary>
public record AgentRatingsResultDto
{
    public Guid AgentId { get; init; }
    public double? AverageRating { get; init; }
    public int TotalRatings { get; init; }
    public List<AgentRatingDto> Ratings { get; init; } = [];
}
