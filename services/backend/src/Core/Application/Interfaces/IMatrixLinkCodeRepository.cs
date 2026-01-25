using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Repository for managing Matrix link verification codes.
/// </summary>
public interface IMatrixLinkCodeRepository
{
    /// <summary>
    /// Gets a link code by its ID.
    /// </summary>
    Task<MatrixLinkCodeEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a link code by code string, regardless of validity state.
    /// </summary>
    Task<MatrixLinkCodeEntity?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a valid (not expired, not used) link code by code string.
    /// </summary>
    Task<MatrixLinkCodeEntity?> GetValidByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active (not expired, not used) code for a user.
    /// </summary>
    Task<MatrixLinkCodeEntity?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new link code.
    /// </summary>
    Task CreateAsync(MatrixLinkCodeEntity linkCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing link code.
    /// </summary>
    Task UpdateAsync(MatrixLinkCodeEntity linkCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a code string is already in use (even if expired).
    /// </summary>
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes expired codes older than the specified date.
    /// </summary>
    Task DeleteExpiredCodesAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
