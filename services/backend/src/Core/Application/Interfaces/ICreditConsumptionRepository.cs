using Domain.Entities;

namespace Application.Interfaces;

public interface ICreditConsumptionRepository
{
    Task<CreditConsumptionEntity> CreateAsync(CreditConsumptionEntity creditConsumption, CancellationToken cancellationToken = default);
    Task<CreditConsumptionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<CreditConsumptionEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CreditConsumptionEntity>> GetRecentByUserIdAsync(Guid userId, int days, CancellationToken cancellationToken = default);
    Task<List<CreditConsumptionEntity>> GetByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<int> GetTotalConsumedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetConsumptionByTypeAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
}