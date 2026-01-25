using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public interface ISubscriptionPlanRepository
{
    Task<SubscriptionPlanEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SubscriptionPlanEntity?> GetByPlanAsync(SubscriptionPlanEnum planEnum,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlanEntity?> GetByStripePriceIdAsync(string stripePriceId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<SubscriptionPlanEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SubscriptionPlanEntity> CreateAsync(SubscriptionPlanEntity plan,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(SubscriptionPlanEntity plan, CancellationToken cancellationToken = default);
}