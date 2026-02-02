using Domain.Entities;

namespace Application.Interfaces;

public interface IUserCreditsRepository
{
    Task<UserCreditsEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserCreditsEntity> CreateAsync(UserCreditsEntity userCredits, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserCreditsEntity userCredits, CancellationToken cancellationToken = default);

    Task UpdateWithVersionCheckAsync(UserCreditsEntity userCredits, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets or creates a UserCreditsEntity for the given user.
    ///     If no record exists, creates one with 0 available credits.
    /// </summary>
    Task<UserCreditsEntity> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken = default);
}
