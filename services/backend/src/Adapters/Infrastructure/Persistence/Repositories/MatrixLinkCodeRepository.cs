using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class MatrixLinkCodeRepository(ApplicationDbContext db) : IMatrixLinkCodeRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<MatrixLinkCodeEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.MatrixLinkCodes.FindAsync([id], cancellationToken);
    }

    public async Task<MatrixLinkCodeEntity?> GetValidByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _db.MatrixLinkCodes
            .FirstOrDefaultAsync(c => c.Code == code && c.ExpiresAt > now && c.UsedAt == null, cancellationToken);
    }

    public async Task<MatrixLinkCodeEntity?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _db.MatrixLinkCodes
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ExpiresAt > now && c.UsedAt == null, cancellationToken);
    }

    public async Task CreateAsync(MatrixLinkCodeEntity linkCode, CancellationToken cancellationToken = default)
    {
        _db.MatrixLinkCodes.Add(linkCode);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MatrixLinkCodeEntity linkCode, CancellationToken cancellationToken = default)
    {
        _db.MatrixLinkCodes.Update(linkCode);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _db.MatrixLinkCodes
            .AnyAsync(c => c.Code == code, cancellationToken);
    }

    public async Task DeleteExpiredCodesAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        await _db.MatrixLinkCodes
            .Where(c => c.ExpiresAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
