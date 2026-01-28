using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class WorkspaceMemberRepository(ApplicationDbContext dbContext) : IWorkspaceMemberRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<WorkspaceMemberEntity?> GetAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkspaceMembers
            .Include(m => m.User)
            .Include(m => m.InvitedBy)
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken);
    }

    public async Task<List<WorkspaceMemberEntity>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkspaceMembers
            .Include(m => m.User)
            .Include(m => m.InvitedBy)
            .Where(m => m.WorkspaceId == workspaceId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkspaceMemberEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkspaceMembers
            .Include(m => m.Workspace)
            .Where(m => m.UserId == userId && m.LeftAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId && m.LeftAt == null, cancellationToken);
    }

    public async Task<bool> HasRoleAsync(Guid workspaceId, Guid userId, WorkspaceRole minimumRole, CancellationToken cancellationToken)
    {
        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId && m.LeftAt == null, cancellationToken);

        if (member == null)
            return false;

        // Lower enum value = higher privilege (Owner=0, Admin=1, Member=2)
        return member.Role <= minimumRole;
    }

    public async Task<WorkspaceMemberEntity> CreateAsync(WorkspaceMemberEntity member, CancellationToken cancellationToken)
    {
        _dbContext.WorkspaceMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return member;
    }

    public async Task UpdateAsync(WorkspaceMemberEntity member, CancellationToken cancellationToken)
    {
        _dbContext.WorkspaceMembers.Update(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken);

        if (member != null)
        {
            _dbContext.WorkspaceMembers.Remove(member);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
