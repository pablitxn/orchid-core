using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class WorkflowRepository(ApplicationDbContext dbContext) : IWorkflowRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<WorkflowEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<WorkflowEntity>().FindAsync([id], cancellationToken);
    }

    public async Task<List<WorkflowEntity>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Set<WorkflowEntity>().ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(WorkflowEntity workflow, CancellationToken cancellationToken)
    {
        _dbContext.Set<WorkflowEntity>().Add(workflow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddUserWorkflowAsync(UserWorkflowEntity userWorkflow, CancellationToken cancellationToken)
    {
        _dbContext.Set<UserWorkflowEntity>().Add(userWorkflow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UserHasWorkflowAsync(Guid userId, Guid workflowId, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<UserWorkflowEntity>()
            .AnyAsync(uw => uw.UserId == userId && uw.WorkflowId == workflowId, cancellationToken);
    }
}
