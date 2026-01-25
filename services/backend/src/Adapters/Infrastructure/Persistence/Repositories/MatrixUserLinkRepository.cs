using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class MatrixUserLinkRepository(ApplicationDbContext db) : IMatrixUserLinkRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<MatrixUserLinkEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.MatrixUserLinks.FindAsync([id], cancellationToken);
    }

    public async Task<MatrixUserLinkEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.MatrixUserLinks
            .FirstOrDefaultAsync(l => l.UserId == userId && l.IsActive, cancellationToken);
    }

    public async Task<MatrixUserLinkEntity?> GetByMatrixUserIdAsync(string matrixUserId, CancellationToken cancellationToken = default)
    {
        return await _db.MatrixUserLinks
            .FirstOrDefaultAsync(l => l.MatrixUserId == matrixUserId && l.IsActive, cancellationToken);
    }

    public async Task CreateAsync(MatrixUserLinkEntity link, CancellationToken cancellationToken = default)
    {
        _db.MatrixUserLinks.Add(link);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MatrixUserLinkEntity link, CancellationToken cancellationToken = default)
    {
        _db.MatrixUserLinks.Update(link);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsByMatrixUserIdAsync(string matrixUserId, CancellationToken cancellationToken = default)
    {
        return await _db.MatrixUserLinks
            .AnyAsync(l => l.MatrixUserId == matrixUserId && l.IsActive, cancellationToken);
    }

    public async Task<bool> UserHasActiveLinkAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.MatrixUserLinks
            .AnyAsync(l => l.UserId == userId && l.IsActive, cancellationToken);
    }
}
