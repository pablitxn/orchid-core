using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ServiceAccountRepository(ApplicationDbContext db) : IServiceAccountRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<ServiceAccountEntity?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return await _db.ServiceAccounts
            .FirstOrDefaultAsync(sa => sa.ClientId == clientId, cancellationToken);
    }

    public async Task<ServiceAccountEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.ServiceAccounts.FindAsync([id], cancellationToken);
    }

    public async Task<ServiceAccountEntity> CreateAsync(ServiceAccountEntity entity, CancellationToken cancellationToken = default)
    {
        _db.ServiceAccounts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ServiceAccountEntity entity, CancellationToken cancellationToken = default)
    {
        _db.ServiceAccounts.Update(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
