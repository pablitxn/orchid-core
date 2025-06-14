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
}