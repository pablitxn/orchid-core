using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class CreditPackageRepository(ApplicationDbContext db) : ICreditPackageRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<CreditPackageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.CreditPackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<CreditPackageEntity?> GetByStripePriceIdAsync(string stripePriceId, CancellationToken cancellationToken = default)
    {
        return await _db.CreditPackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StripePriceId == stripePriceId, cancellationToken);
    }

    public async Task<IEnumerable<CreditPackageEntity>> GetActivePackagesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.CreditPackages
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreditPackageEntity> CreateAsync(CreditPackageEntity package, CancellationToken cancellationToken = default)
    {
        _db.CreditPackages.Add(package);
        await _db.SaveChangesAsync(cancellationToken);
        return package;
    }

    public async Task UpdateAsync(CreditPackageEntity package, CancellationToken cancellationToken = default)
    {
        package.UpdatedAt = DateTime.UtcNow;

        var entry = _db.Entry(package);
        if (entry.State == EntityState.Detached)
        {
            _db.CreditPackages.Attach(package);
            entry.State = EntityState.Modified;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
