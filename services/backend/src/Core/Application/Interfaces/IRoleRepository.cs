using Domain.Entities;

namespace Application.Interfaces;

public interface IRoleRepository
{
    Task<RoleEntity?> GetByNameAsync(string name);
    Task<RoleEntity> CreateAsync(RoleEntity role);
    Task AssignRoleToUserAsync(Guid userId, Guid roleId);

    /// <summary>
    /// Gets all roles assigned to a user.
    /// </summary>
    Task<IReadOnlyList<RoleEntity>> GetRolesForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role assignment from a user.
    /// </summary>
    Task RemoveRoleFromUserAsync(Guid userId, Guid roleId);
}