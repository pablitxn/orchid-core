using Domain.Entities;

namespace Application.Interfaces;

public interface IPluginRepository
{
    Task<PluginEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PluginEntity?> GetBySystemNameAsync(string systemName, CancellationToken cancellationToken);
    Task<List<PluginEntity>> ListAsync(CancellationToken cancellationToken);
    Task<List<PluginEntity>> ListActiveAsync(CancellationToken cancellationToken);
    Task<List<PluginEntity>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task CreateAsync(PluginEntity plugin, CancellationToken cancellationToken);
    Task UpdateAsync(PluginEntity plugin, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets plugins associated with a specific agent
    /// </summary>
    Task<List<PluginEntity>> ListByAgentAsync(Guid agentId, CancellationToken cancellationToken);
}