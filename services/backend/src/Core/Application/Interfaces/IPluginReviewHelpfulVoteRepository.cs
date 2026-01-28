using Domain.Entities;

namespace Application.Interfaces;

public interface IPluginReviewHelpfulVoteRepository
{
    /// <summary>
    /// Checks if a user has already voted helpful for a specific review
    /// </summary>
    Task<bool> ExistsAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new helpful vote
    /// </summary>
    Task CreateAsync(PluginReviewHelpfulVoteEntity vote, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a helpful vote
    /// </summary>
    Task DeleteAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken);
}
