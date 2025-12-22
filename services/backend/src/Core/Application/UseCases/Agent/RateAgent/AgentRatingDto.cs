namespace Application.UseCases.Agent.RateAgent;

/// <summary>
/// Data transfer object for an agent rating
/// </summary>
public record AgentRatingDto
{
    public Guid Id { get; init; }
    public Guid AgentId { get; init; }
    public Guid UserId { get; init; }
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// The new average rating for the agent after this rating was submitted
    /// </summary>
    public double? NewAverageRating { get; init; }
}
