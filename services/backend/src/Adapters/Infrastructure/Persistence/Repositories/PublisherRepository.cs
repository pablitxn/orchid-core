using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PublisherRepository(ApplicationDbContext dbContext) : IPublisherRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<PublisherEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Publishers
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PublisherEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Publishers
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Publishers
            .AnyAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<List<PublisherEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Publishers
            .Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(PublisherEntity publisher, CancellationToken cancellationToken = default)
    {
        _dbContext.Publishers.Add(publisher);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PublisherEntity publisher, CancellationToken cancellationToken = default)
    {
        publisher.UpdatedAt = DateTime.UtcNow;
        _dbContext.Publishers.Update(publisher);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
