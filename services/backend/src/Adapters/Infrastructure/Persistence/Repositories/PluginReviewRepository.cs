using Application.Common.Models;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PluginReviewRepository(ApplicationDbContext dbContext) : IPluginReviewRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<PluginReviewEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.PluginReviews
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);
    }

    public async Task<PluginReviewEntity?> GetByPluginAndUserAsync(Guid pluginId, Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.PluginReviews
            .FirstOrDefaultAsync(r => r.PluginId == pluginId && r.UserId == userId && !r.IsDeleted, cancellationToken);
    }

    public async Task<PaginatedResult<PluginReviewEntity>> ListByPluginAsync(
        Guid pluginId,
        int page,
        int pageSize,
        string sortBy,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PluginReviews
            .Include(r => r.User)
            .Where(r => r.PluginId == pluginId && !r.IsDeleted);

        query = sortBy.ToLowerInvariant() switch
        {
            "helpful" => query.OrderByDescending(r => r.HelpfulCount).ThenByDescending(r => r.CreatedAt),
            "oldest" => query.OrderBy(r => r.CreatedAt),
            "highest" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            "lowest" => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<PluginReviewEntity>(items, total, page, pageSize);
    }

    public async Task<double?> GetAverageRatingAsync(Guid pluginId, CancellationToken cancellationToken)
    {
        var hasReviews = await _dbContext.PluginReviews
            .AnyAsync(r => r.PluginId == pluginId && !r.IsDeleted, cancellationToken);

        if (!hasReviews)
            return null;

        return await _dbContext.PluginReviews
            .Where(r => r.PluginId == pluginId && !r.IsDeleted)
            .AverageAsync(r => r.Rating, cancellationToken);
    }

    public async Task<int> GetReviewCountAsync(Guid pluginId, CancellationToken cancellationToken)
    {
        return await _dbContext.PluginReviews
            .CountAsync(r => r.PluginId == pluginId && !r.IsDeleted, cancellationToken);
    }

    public async Task CreateAsync(PluginReviewEntity review, CancellationToken cancellationToken)
    {
        _dbContext.PluginReviews.Add(review);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PluginReviewEntity review, CancellationToken cancellationToken)
    {
        _dbContext.PluginReviews.Update(review);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var review = await _dbContext.PluginReviews.FindAsync([id], cancellationToken);
        if (review is not null)
        {
            review.IsDeleted = true;
            review.DeletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
