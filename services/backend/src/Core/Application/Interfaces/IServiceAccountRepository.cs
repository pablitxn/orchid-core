using Domain.Entities;

namespace Application.Interfaces;

public interface IServiceAccountRepository
{
    Task<ServiceAccountEntity?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<ServiceAccountEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceAccountEntity> CreateAsync(ServiceAccountEntity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ServiceAccountEntity entity, CancellationToken cancellationToken = default);
}
