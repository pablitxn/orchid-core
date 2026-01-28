using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PluginReviewFlagRepository(ApplicationDbContext dbContext) : IPluginReviewFlagRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<bool> ExistsAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.PluginReviewFlags
            .AnyAsync(f => f.ReviewId == reviewId && f.UserId == userId, cancellationToken);
    }

    public async Task CreateAsync(PluginReviewFlagEntity flag, CancellationToken cancellationToken)
    {
        _dbContext.PluginReviewFlags.Add(flag);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetFlagCountAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        return await _dbContext.PluginReviewFlags
            .CountAsync(f => f.ReviewId == reviewId, cancellationToken);
    }
}
