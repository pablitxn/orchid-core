using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public interface IWorkspaceMemberRepository
{
    Task<WorkspaceMemberEntity?> GetAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken);

    Task<List<WorkspaceMemberEntity>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<List<WorkspaceMemberEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a user has at least the specified role in the workspace.
    /// </summary>
    Task<bool> HasRoleAsync(Guid workspaceId, Guid userId, WorkspaceRole minimumRole, CancellationToken cancellationToken);

    Task<WorkspaceMemberEntity> CreateAsync(WorkspaceMemberEntity member, CancellationToken cancellationToken);

    Task UpdateAsync(WorkspaceMemberEntity member, CancellationToken cancellationToken);

    Task RemoveAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken);
}
