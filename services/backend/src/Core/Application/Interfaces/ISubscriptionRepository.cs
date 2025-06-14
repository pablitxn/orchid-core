using Domain.Entities;

namespace Application.Interfaces;

public interface ISubscriptionRepository
{
    Task<SubscriptionEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<SubscriptionEntity> CreateAsync(SubscriptionEntity subscription,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(SubscriptionEntity subscription, CancellationToken cancellationToken = default);
    
    Task UpdateWithVersionCheckAsync(SubscriptionEntity subscription, int expectedVersion, CancellationToken cancellationToken = default);
}