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

    public Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<UserEntity?> GetByIdAsync(Guid id)
    {
        return await _db.Users.FindAsync(id);
    }

    public async Task UpdateAsync(UserEntity user)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }
}