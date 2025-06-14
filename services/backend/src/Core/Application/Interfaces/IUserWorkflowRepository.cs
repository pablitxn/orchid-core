using Domain.Entities;

namespace Application.Interfaces;

public interface IUserWorkflowRepository
{
    Task<UserWorkflowEntity> CreateAsync(UserWorkflowEntity userWorkflow,
        CancellationToken cancellationToken = default);

    Task<UserWorkflowEntity?> GetByIdAsync(Guid userId, Guid workflowId, CancellationToken cancellationToken = default);
    Task<List<UserWorkflowEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<UserWorkflowEntity>> GetByWorkflowIdAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid userId, Guid workflowId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid workflowId, CancellationToken cancellationToken = default);
}