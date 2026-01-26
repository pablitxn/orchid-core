using Domain.Entities;

namespace Application.Interfaces;

public interface ICreditPackageRepository
{
    Task<CreditPackageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CreditPackageEntity?> GetByStripePriceIdAsync(string stripePriceId, CancellationToken cancellationToken = default);

    Task<IEnumerable<CreditPackageEntity>> GetActivePackagesAsync(CancellationToken cancellationToken = default);

    Task<CreditPackageEntity> CreateAsync(CreditPackageEntity package, CancellationToken cancellationToken = default);

    Task UpdateAsync(CreditPackageEntity package, CancellationToken cancellationToken = default);
}
