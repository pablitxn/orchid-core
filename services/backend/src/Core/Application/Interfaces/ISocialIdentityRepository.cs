using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Repository interface for managing user social identities.
/// </summary>
public interface ISocialIdentityRepository
{
    /// <summary>
    /// Gets a social identity by its ID.
    /// </summary>
    Task<UserSocialIdentityEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets a social identity by user ID and provider.
    /// </summary>
    Task<UserSocialIdentityEntity?> GetByProviderAsync(Guid userId, string provider, CancellationToken ct = default);

    /// <summary>
    /// Gets all social identities for a user.
    /// </summary>
    Task<IReadOnlyList<UserSocialIdentityEntity>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets a social identity by provider and provider user ID.
    /// </summary>
    Task<UserSocialIdentityEntity?> GetByProviderUserIdAsync(string provider, string providerUserId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new social identity.
    /// </summary>
    Task<UserSocialIdentityEntity> CreateAsync(UserSocialIdentityEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing social identity.
    /// </summary>
    Task UpdateAsync(UserSocialIdentityEntity entity, CancellationToken ct = default);
}
