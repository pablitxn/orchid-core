using Domain.Entities;

namespace Application.Interfaces;

public interface IUserPluginRepository
{
    Task<UserPluginEntity?> GetByUserAndPluginAsync(Guid userId, Guid pluginId, CancellationToken cancellationToken);
    Task<List<UserPluginEntity>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> UserOwnsPluginAsync(Guid userId, Guid pluginId, CancellationToken cancellationToken);
    Task CreateAsync(UserPluginEntity userPlugin, CancellationToken cancellationToken);
    Task UpdateAsync(UserPluginEntity userPlugin, CancellationToken cancellationToken);
    Task<List<UserPluginEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task DeleteByUserAndPluginAsync(Guid userId, Guid pluginId, CancellationToken cancellationToken);
}