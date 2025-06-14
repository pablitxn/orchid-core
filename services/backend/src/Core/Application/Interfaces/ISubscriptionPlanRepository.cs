using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public interface ISubscriptionPlanRepository
{
    Task<SubscriptionPlanEntity?> GetByPlanAsync(SubscriptionPlanEnum planEnum,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlanEntity> CreateAsync(SubscriptionPlanEntity plan,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(SubscriptionPlanEntity plan, CancellationToken cancellationToken = default);
}