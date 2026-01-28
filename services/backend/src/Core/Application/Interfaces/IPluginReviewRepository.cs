using Application.Common.Models;
using Domain.Entities;

namespace Application.Interfaces;

public interface IPluginReviewRepository
{
    /// <summary>
    /// Gets a review by its ID
    /// </summary>
    Task<PluginReviewEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a user's review for a specific plugin
    /// </summary>
    Task<PluginReviewEntity?> GetByPluginAndUserAsync(Guid pluginId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets paginated reviews for a plugin with sorting
    /// </summary>
    Task<PaginatedResult<PluginReviewEntity>> ListByPluginAsync(
        Guid pluginId,
        int page,
        int pageSize,
        string sortBy,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the average rating for a plugin
    /// </summary>
    Task<double?> GetAverageRatingAsync(Guid pluginId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the total number of reviews for a plugin
    /// </summary>
    Task<int> GetReviewCountAsync(Guid pluginId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new review
    /// </summary>
    Task CreateAsync(PluginReviewEntity review, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing review
    /// </summary>
    Task UpdateAsync(PluginReviewEntity review, CancellationToken cancellationToken);

    /// <summary>
    /// Soft deletes a review
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
