using Domain.Entities;

namespace Application.Interfaces;

public interface IPluginVersionRepository
{
    Task<PluginVersionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PluginVersionEntity?> GetByPluginAndVersionAsync(Guid pluginId, string version, CancellationToken cancellationToken = default);

    Task<PluginVersionEntity?> GetLatestAsync(Guid pluginId, bool includePreRelease = false, CancellationToken cancellationToken = default);

    Task<List<PluginVersionEntity>> ListByPluginAsync(Guid pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists versions compatible with the current version (same major version) for rollback purposes.
    /// </summary>
    Task<List<PluginVersionEntity>> ListCompatibleVersionsAsync(Guid pluginId, string currentVersion, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid pluginId, string version, CancellationToken cancellationToken = default);

    Task CreateAsync(PluginVersionEntity version, CancellationToken cancellationToken = default);

    Task UpdateAsync(PluginVersionEntity version, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
