using Domain.Entities;

namespace Application.Interfaces;

public interface IFeatureFlagRepository
{
    Task<FeatureFlagEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<FeatureFlagEntity?> GetByKeyAsync(string key, CancellationToken cancellationToken);
    Task<List<FeatureFlagEntity>> ListAsync(CancellationToken cancellationToken);
    Task<List<FeatureFlagEntity>> ListEnabledAsync(CancellationToken cancellationToken);
    Task CreateAsync(FeatureFlagEntity featureFlag, CancellationToken cancellationToken);
    Task UpdateAsync(FeatureFlagEntity featureFlag, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
