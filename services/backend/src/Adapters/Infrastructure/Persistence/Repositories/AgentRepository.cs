using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class AgentRepository(ApplicationDbContext dbContext) : IAgentRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<AgentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Agents.FindAsync([id], cancellationToken);
    }

    public async Task<List<AgentEntity>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Agents
            .Where(a => !a.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(AgentEntity agent, CancellationToken cancellationToken)
    {
        _dbContext.Agents.Add(agent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AgentEntity agent, CancellationToken cancellationToken)
    {
        _dbContext.Agents.Update(agent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var agent = await _dbContext.Agents.FindAsync([id], cancellationToken);
        if (agent is not null)
        {
            _dbContext.Agents.Remove(agent);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<AgentEntity>> ListRecycleBinAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Agents
            .Where(a => a.IsDeleted && a.IsInRecycleBin)
            .OrderByDescending(a => a.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AgentEntity>> ListExpiredRecycleBinAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _dbContext.Agents
            .Where(a => a.IsInRecycleBin && a.RecycleBinExpiresAt != null && a.RecycleBinExpiresAt <= now)
            .ToListAsync(cancellationToken);
    }

    public async Task<AgentEntity?> GetWithPluginsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Agents
            .Include(a => a.PersonalityTemplate)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
    }
}