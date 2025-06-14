using Domain.Entities;

namespace Application.Interfaces;

public interface ICostConfigurationRepository
{
    Task<CostConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<CostConfigurationEntity?> GetCostForTypeAsync(string costType, Guid? resourceId = null, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<CostConfigurationEntity>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    Task<IEnumerable<CostConfigurationEntity>> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    
    Task<CostConfigurationEntity> CreateAsync(CostConfigurationEntity configuration, CancellationToken cancellationToken = default);
    
    Task UpdateAsync(CostConfigurationEntity configuration, CancellationToken cancellationToken = default);
    
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}