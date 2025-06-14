using Domain.Entities;

namespace Application.Interfaces;

public interface IRoleRepository
{
    Task<RoleEntity?> GetByNameAsync(string name);
    Task<RoleEntity> CreateAsync(RoleEntity role);
    Task AssignRoleToUserAsync(Guid userId, Guid roleId);
}