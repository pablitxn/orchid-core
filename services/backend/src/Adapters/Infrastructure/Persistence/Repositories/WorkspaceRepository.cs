using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class WorkspaceRepository(ApplicationDbContext dbContext) : IWorkspaceRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<WorkspaceEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .Include(w => w.Owner)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<WorkspaceEntity?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .Include(w => w.Owner)
            .FirstOrDefaultAsync(w => w.Slug == slug && w.DeletedAt == null, cancellationToken);
    }

    public async Task<WorkspaceEntity?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .Include(w => w.Owner)
            .Include(w => w.Members.Where(m => m.LeftAt == null))
                .ThenInclude(m => m.User)
            .Include(w => w.Members.Where(m => m.LeftAt == null))
                .ThenInclude(m => m.InvitedBy)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<List<WorkspaceEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .Include(w => w.Owner)
            .Include(w => w.Members)
            .Where(w => w.DeletedAt == null && w.Members.Any(m => m.UserId == userId && m.LeftAt == null))
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkspaceEntity?> GetPersonalWorkspaceAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.OwnerId == userId && w.IsPersonal && w.DeletedAt == null, cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .AnyAsync(w => w.Slug == slug && w.DeletedAt == null, cancellationToken);
    }

    public async Task<WorkspaceEntity> CreateAsync(WorkspaceEntity workspace, CancellationToken cancellationToken)
    {
        _dbContext.Workspaces.Add(workspace);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return workspace;
    }

    public async Task UpdateAsync(WorkspaceEntity workspace, CancellationToken cancellationToken)
    {
        _dbContext.Workspaces.Update(workspace);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var workspace = await _dbContext.Workspaces.FindAsync([id], cancellationToken);
        if (workspace != null)
        {
            _dbContext.Workspaces.Remove(workspace);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
