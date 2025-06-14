using Microsoft.SemanticKernel;

namespace Application.Interfaces;

/// <summary>
/// Service responsible for dynamically loading plugins based on agent configuration
/// </summary>
public interface IAgentPluginLoader
{
    /// <summary>
    /// Loads and registers plugins for a specific agent into the kernel
    /// </summary>
    /// <param name="agentId">The ID of the agent whose plugins should be loaded</param>
    /// <param name="kernel">The Semantic Kernel instance to load plugins into</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of loaded plugin names</returns>
    Task<IReadOnlyList<string>> LoadAgentPluginsAsync(Guid agentId, Kernel kernel, CancellationToken cancellationToken = default);
}