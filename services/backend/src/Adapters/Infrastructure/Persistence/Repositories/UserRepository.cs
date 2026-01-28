using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UserRepository(ApplicationDbContext db) : IUserRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<UserEntity> GetByEmailAsync(string email)
    {
        var user = await _db.Users
            .SingleOrDefaultAsync(u => u.Email == email);

        return user!;
    }

    public async Task<UserEntity> CreateAsync(UserEntity newUser)
    {
        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();
        return newUser;
    }

    public async Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Users.FindAsync(id, cancellationToken);
    }

    public async Task UpdateAsync(UserEntity user)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    public async Task<UserEntity?> GetByAuth0UserIdAsync(string auth0UserId, CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Auth0UserId == auth0UserId, cancellationToken);
    }
}