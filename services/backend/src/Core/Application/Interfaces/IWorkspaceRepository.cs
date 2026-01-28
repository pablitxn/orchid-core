using Domain.Entities;

namespace Application.Interfaces;

public interface IWorkspaceRepository
{
    Task<WorkspaceEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkspaceEntity?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<WorkspaceEntity?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all workspaces where the user is a member (including owned).
    /// </summary>
    Task<List<WorkspaceEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the user's personal workspace (auto-created).
    /// </summary>
    Task<WorkspaceEntity?> GetPersonalWorkspaceAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<WorkspaceEntity> CreateAsync(WorkspaceEntity workspace, CancellationToken cancellationToken);

    Task UpdateAsync(WorkspaceEntity workspace, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
