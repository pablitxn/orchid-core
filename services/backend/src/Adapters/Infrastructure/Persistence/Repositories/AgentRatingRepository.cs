using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class AgentRatingRepository(ApplicationDbContext dbContext) : IAgentRatingRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<AgentRatingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.AgentRatings.FindAsync([id], cancellationToken);
    }

    public async Task<AgentRatingEntity?> GetByAgentAndUserAsync(Guid agentId, Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.AgentRatings
            .FirstOrDefaultAsync(r => r.AgentId == agentId && r.UserId == userId, cancellationToken);
    }

    public async Task<List<AgentRatingEntity>> ListByAgentAsync(Guid agentId, CancellationToken cancellationToken)
    {
        return await _dbContext.AgentRatings
            .Where(r => r.AgentId == agentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<double?> GetAverageRatingAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var hasRatings = await _dbContext.AgentRatings
            .AnyAsync(r => r.AgentId == agentId, cancellationToken);

        if (!hasRatings)
            return null;

        return await _dbContext.AgentRatings
            .Where(r => r.AgentId == agentId)
            .AverageAsync(r => r.Rating, cancellationToken);
    }

    public async Task<Dictionary<Guid, double?>> GetAverageRatingsForAgentsAsync(
        IEnumerable<Guid> agentIds,
        CancellationToken cancellationToken)
    {
        var agentIdList = agentIds.ToList();

        if (agentIdList.Count == 0)
            return new Dictionary<Guid, double?>();

        var ratings = await _dbContext.AgentRatings
            .Where(r => agentIdList.Contains(r.AgentId))
            .GroupBy(r => r.AgentId)
            .Select(g => new
            {
                AgentId = g.Key,
                AverageRating = g.Average(r => r.Rating)
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, double?>();

        foreach (var agentId in agentIdList)
        {
            var rating = ratings.FirstOrDefault(r => r.AgentId == agentId);
            result[agentId] = rating?.AverageRating;
        }

        return result;
    }

    public async Task<int> GetRatingCountAsync(Guid agentId, CancellationToken cancellationToken)
    {
        return await _dbContext.AgentRatings
            .CountAsync(r => r.AgentId == agentId, cancellationToken);
    }

    public async Task CreateAsync(AgentRatingEntity rating, CancellationToken cancellationToken)
    {
        _dbContext.AgentRatings.Add(rating);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AgentRatingEntity rating, CancellationToken cancellationToken)
    {
        _dbContext.AgentRatings.Update(rating);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var rating = await _dbContext.AgentRatings.FindAsync([id], cancellationToken);
        if (rating is not null)
        {
            _dbContext.AgentRatings.Remove(rating);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
