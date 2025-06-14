using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class TeamRepository(ApplicationDbContext dbContext) : ITeamRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<TeamEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Teams
            .Include(t => t.TeamAgents)
            .ThenInclude(ta => ta.Agent)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<List<TeamEntity>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Teams
            .Include(t => t.TeamAgents)
            .ThenInclude(ta => ta.Agent)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(TeamEntity team, CancellationToken cancellationToken)
    {
        _dbContext.Teams.Add(team);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TeamEntity team, CancellationToken cancellationToken)
    {
        _dbContext.Teams.Update(team);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var team = await _dbContext.Teams.FindAsync([id], cancellationToken);
        if (team is not null)
        {
            _dbContext.Teams.Remove(team);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}