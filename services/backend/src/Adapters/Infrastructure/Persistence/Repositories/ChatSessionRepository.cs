using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ChatSessionRepository(ApplicationDbContext dbContext) : IChatSessionRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<List<ChatSessionEntity>> ListAsync(
        Guid userId,
        bool archived,
        Guid? agentId,
        Guid? teamId,
        DateTime? startDate,
        DateTime? endDate,
        InteractionType? type,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ChatSessions
            .Where(s => s.UserId == userId && s.IsArchived == archived);

        if (agentId.HasValue) query = query.Where(s => s.AgentId == agentId);

        if (teamId.HasValue) query = query.Where(s => s.TeamId == teamId);

        if (startDate.HasValue) query = query.Where(s => s.CreatedAt >= startDate);

        if (endDate.HasValue) query = query.Where(s => s.CreatedAt <= endDate);

        if (type.HasValue) query = query.Where(s => s.InteractionType == type);

        return await query
            .Include(s => s.Agent)
            .Include(s => s.Team)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ChatSessionEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.ChatSessions
            .Include(s => s.Agent)
            .Include(s => s.Team)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(ChatSessionEntity session, CancellationToken cancellationToken)
    {
        _dbContext.ChatSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(Guid id, bool archived, CancellationToken cancellationToken)
    {
        var session = await _dbContext.ChatSessions.FindAsync(new object[] { id }, cancellationToken);
        if (session is null) return;
        session.IsArchived = archived;
        session.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await _dbContext.ChatSessions.FindAsync(new object[] { id }, cancellationToken);
        if (session is not null)
        {
            _dbContext.ChatSessions.Remove(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.ChatSessions.Where(s => ids.Contains(s.Id)).ToListAsync(cancellationToken);
        _dbContext.ChatSessions.RemoveRange(sessions);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ChatSessionEntity?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        return await _dbContext.ChatSessions
            .Include(s => s.Agent)
            .Include(s => s.Team)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
    }
}