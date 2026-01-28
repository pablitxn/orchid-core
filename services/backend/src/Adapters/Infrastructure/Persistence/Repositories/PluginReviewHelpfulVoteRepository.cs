using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PluginReviewHelpfulVoteRepository(ApplicationDbContext dbContext) : IPluginReviewHelpfulVoteRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<bool> ExistsAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.PluginReviewHelpfulVotes
            .AnyAsync(v => v.ReviewId == reviewId && v.UserId == userId, cancellationToken);
    }

    public async Task CreateAsync(PluginReviewHelpfulVoteEntity vote, CancellationToken cancellationToken)
    {
        _dbContext.PluginReviewHelpfulVotes.Add(vote);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken)
    {
        var vote = await _dbContext.PluginReviewHelpfulVotes
            .FirstOrDefaultAsync(v => v.ReviewId == reviewId && v.UserId == userId, cancellationToken);

        if (vote is not null)
        {
            _dbContext.PluginReviewHelpfulVotes.Remove(vote);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
