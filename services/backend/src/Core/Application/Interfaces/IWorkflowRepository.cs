using Domain.Entities;

namespace Application.Interfaces;

public interface IWorkflowRepository
{
    Task<WorkflowEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<WorkflowEntity>> ListAsync(CancellationToken cancellationToken);
    Task CreateAsync(WorkflowEntity workflow, CancellationToken cancellationToken);
    Task AddUserWorkflowAsync(UserWorkflowEntity userWorkflow, CancellationToken cancellationToken);
    Task<bool> UserHasWorkflowAsync(Guid userId, Guid workflowId, CancellationToken cancellationToken);
}
