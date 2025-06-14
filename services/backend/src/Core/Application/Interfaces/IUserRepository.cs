using Domain.Entities;

namespace Application.Interfaces;

public interface IUserRepository
{
    Task<UserEntity> GetByEmailAsync(string email);
    Task<UserEntity> CreateAsync(UserEntity newUser);
    Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserEntity user);
}