using MediatR;

namespace Application.UseCases.Agent.RateAgent;

/// <summary>
/// Command to rate an agent. Users can rate an agent once and update their rating.
/// </summary>
public class RateAgentCommand(
    Guid agentId,
    Guid userId,
    int rating,
    string? comment = null) : IRequest<AgentRatingDto>
{
    /// <summary>
    /// The ID of the agent being rated
    /// </summary>
    public Guid AgentId { get; set; } = agentId;

    /// <summary>
    /// The ID of the user submitting the rating
    /// </summary>
    public Guid UserId { get; set; } = userId;

    /// <summary>
    /// Rating value from 1 to 5
    /// </summary>
    public int Rating { get; set; } = rating;

    /// <summary>
    /// Optional comment/review
    /// </summary>
    public string? Comment { get; set; } = comment;
}
