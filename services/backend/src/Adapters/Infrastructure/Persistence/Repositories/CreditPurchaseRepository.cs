using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class CreditPurchaseRepository(ApplicationDbContext db) : ICreditPurchaseRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<CreditPurchaseEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.CreditPurchases
            .Include(p => p.CreditPackage)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<CreditPurchaseEntity?> GetByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _db.CreditPurchases
            .Include(p => p.CreditPackage)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StripeCheckoutSessionId == sessionId, cancellationToken);
    }

    public async Task<CreditPurchaseEntity?> GetByStripePaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        return await _db.CreditPurchases
            .Include(p => p.CreditPackage)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken);
    }

    public async Task<IEnumerable<CreditPurchaseEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.CreditPurchases
            .Include(p => p.CreditPackage)
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PurchasedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreditPurchaseEntity> CreateAsync(CreditPurchaseEntity purchase, CancellationToken cancellationToken = default)
    {
        _db.CreditPurchases.Add(purchase);
        await _db.SaveChangesAsync(cancellationToken);
        return purchase;
    }

    public async Task UpdateAsync(CreditPurchaseEntity purchase, CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(purchase);
        if (entry.State == EntityState.Detached)
        {
            _db.CreditPurchases.Attach(purchase);
            entry.State = EntityState.Modified;
        }

        // Ignore navigation properties to avoid tracking conflicts
        entry.Reference(p => p.CreditPackage).IsModified = false;
        entry.Reference(p => p.User).IsModified = false;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
