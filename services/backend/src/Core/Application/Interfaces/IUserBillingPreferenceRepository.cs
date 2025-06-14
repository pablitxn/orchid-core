using Domain.Entities;

namespace Application.Interfaces;

public interface IUserBillingPreferenceRepository
{
    Task<UserBillingPreferenceEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserBillingPreferenceEntity> CreateAsync(UserBillingPreferenceEntity preference, CancellationToken cancellationToken = default);
    Task<UserBillingPreferenceEntity> UpdateAsync(UserBillingPreferenceEntity preference, CancellationToken cancellationToken = default);
}