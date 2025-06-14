using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class MessageCostRepository : IMessageCostRepository
{
    private readonly ApplicationDbContext _context;

    public MessageCostRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MessageCostEntity> CreateAsync(MessageCostEntity messageCost, CancellationToken cancellationToken = default)
    {
        _context.MessageCosts.Add(messageCost);
        await _context.SaveChangesAsync(cancellationToken);
        return messageCost;
    }

    public async Task<MessageCostEntity?> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await _context.MessageCosts
            .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);
    }

    public async Task<IEnumerable<MessageCostEntity>> GetByUserIdAsync(Guid userId, int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = _context.MessageCosts
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt);

        if (limit.HasValue)
            query = (IOrderedQueryable<MessageCostEntity>)query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalCostByUserIdAsync(Guid userId, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _context.MessageCosts
            .Where(m => m.UserId == userId);

        if (since.HasValue)
            query = query.Where(m => m.CreatedAt >= since.Value);

        return await query.SumAsync(m => m.TotalCredits + m.AdditionalCredits, cancellationToken);
    }
}