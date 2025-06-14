using Domain.Entities;

namespace Application.Interfaces;

public interface IUserCreditLimitRepository
{
    Task<UserCreditLimitEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<UserCreditLimitEntity>> GetByUserIdAsync(Guid userId, bool activeOnly = true, CancellationToken cancellationToken = default);
    
    Task<UserCreditLimitEntity?> GetByUserAndTypeAsync(Guid userId, string limitType, string? resourceType = null, CancellationToken cancellationToken = default);
    
    Task<UserCreditLimitEntity> CreateAsync(UserCreditLimitEntity limit, CancellationToken cancellationToken = default);
    
    Task UpdateAsync(UserCreditLimitEntity limit, CancellationToken cancellationToken = default);
    
    Task UpdateWithVersionCheckAsync(UserCreditLimitEntity limit, int expectedVersion, CancellationToken cancellationToken = default);
    
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> CheckLimitsAsync(Guid userId, int requestedCredits, string? resourceType = null, CancellationToken cancellationToken = default);
    
    Task ConsumeLimitsAsync(Guid userId, int credits, string? resourceType = null, CancellationToken cancellationToken = default);
}