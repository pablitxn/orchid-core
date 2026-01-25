using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Repository for managing Matrix user links.
/// </summary>
public interface IMatrixUserLinkRepository
{
    /// <summary>
    /// Gets a link by its ID.
    /// </summary>
    Task<MatrixUserLinkEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active link for a user.
    /// </summary>
    Task<MatrixUserLinkEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a link by Matrix user ID.
    /// </summary>
    Task<MatrixUserLinkEntity?> GetByMatrixUserIdAsync(string matrixUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new Matrix user link.
    /// </summary>
    Task CreateAsync(MatrixUserLinkEntity link, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing link.
    /// </summary>
    Task UpdateAsync(MatrixUserLinkEntity link, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a Matrix user ID is already linked.
    /// </summary>
    Task<bool> ExistsByMatrixUserIdAsync(string matrixUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user already has an active Matrix link.
    /// </summary>
    Task<bool> UserHasActiveLinkAsync(Guid userId, CancellationToken cancellationToken = default);
}
