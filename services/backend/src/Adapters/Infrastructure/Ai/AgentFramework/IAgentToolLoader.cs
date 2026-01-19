using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.AgentFramework;

/// <summary>
/// Service responsible for dynamically loading tools based on agent configuration.
/// </summary>
public interface IAgentToolLoader
{
    /// <summary>
    /// Loads and returns the tools configured for a specific agent.
    /// </summary>
    /// <param name="agentId">The ID of the agent whose tools should be loaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of AIFunction tools for the agent.</returns>
    Task<IReadOnlyList<AIFunction>> LoadAgentToolsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);
}
