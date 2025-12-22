using Domain.Entities;

namespace Application.Interfaces;

public interface IAgentRatingRepository
{
    /// <summary>
    /// Gets a rating by its ID
    /// </summary>
    Task<AgentRatingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a user's rating for a specific agent
    /// </summary>
    Task<AgentRatingEntity?> GetByAgentAndUserAsync(Guid agentId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all ratings for an agent
    /// </summary>
    Task<List<AgentRatingEntity>> ListByAgentAsync(Guid agentId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the average rating for an agent
    /// </summary>
    Task<double?> GetAverageRatingAsync(Guid agentId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets average ratings for multiple agents in a single query (batch operation)
    /// </summary>
    Task<Dictionary<Guid, double?>> GetAverageRatingsForAgentsAsync(
        IEnumerable<Guid> agentIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the total number of ratings for an agent
    /// </summary>
    Task<int> GetRatingCountAsync(Guid agentId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new rating
    /// </summary>
    Task CreateAsync(AgentRatingEntity rating, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing rating
    /// </summary>
    Task UpdateAsync(AgentRatingEntity rating, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a rating
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
