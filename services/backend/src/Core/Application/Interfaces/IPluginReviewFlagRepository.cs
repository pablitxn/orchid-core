using Domain.Entities;

namespace Application.Interfaces;

public interface IPluginReviewFlagRepository
{
    /// <summary>
    /// Checks if a user has already flagged a specific review
    /// </summary>
    Task<bool> ExistsAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new flag
    /// </summary>
    Task CreateAsync(PluginReviewFlagEntity flag, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the total number of flags for a review
    /// </summary>
    Task<int> GetFlagCountAsync(Guid reviewId, CancellationToken cancellationToken);
}
