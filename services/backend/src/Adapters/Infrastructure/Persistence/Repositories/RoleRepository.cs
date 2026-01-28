using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class RoleRepository(ApplicationDbContext db) : IRoleRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<RoleEntity?> GetByNameAsync(string name)
    {
        return await _db.Roles.SingleOrDefaultAsync(r => r.Name == name);
    }

    public async Task<RoleEntity> CreateAsync(RoleEntity role)
    {
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    public async Task AssignRoleToUserAsync(Guid userId, Guid roleId)
    {
        _db.UserRoles.Add(new UserRoleEntity { UserId = userId, RoleId = roleId });
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<RoleEntity>> GetRolesForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveRoleFromUserAsync(Guid userId, Guid roleId)
    {
        var userRole = await _db.UserRoles
            .SingleOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole != null)
        {
            _db.UserRoles.Remove(userRole);
            await _db.SaveChangesAsync();
        }
    }
}