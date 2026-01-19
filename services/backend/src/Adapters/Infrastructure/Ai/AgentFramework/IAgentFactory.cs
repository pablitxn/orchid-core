using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.AgentFramework;

/// <summary>
/// Factory for creating AI agents with optional model override.
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Creates an AI agent with the default model.
    /// </summary>
    /// <param name="instructions">System instructions for the agent.</param>
    /// <param name="name">Optional agent name.</param>
    /// <param name="tools">Optional list of tools the agent can use.</param>
    /// <returns>A configured AI agent.</returns>
    AIAgent CreateAgent(
        string instructions,
        string? name = null,
        IReadOnlyList<AIFunction>? tools = null);

    /// <summary>
    /// Creates an AI agent with a specific model override.
    /// </summary>
    /// <param name="instructions">System instructions for the agent.</param>
    /// <param name="modelId">The specific model to use (overrides default).</param>
    /// <param name="name">Optional agent name.</param>
    /// <param name="tools">Optional list of tools the agent can use.</param>
    /// <returns>A configured AI agent.</returns>
    AIAgent CreateAgentWithModel(
        string instructions,
        string modelId,
        string? name = null,
        IReadOnlyList<AIFunction>? tools = null);

    /// <summary>
    /// Gets an IChatClient for a specific model.
    /// </summary>
    /// <param name="modelId">The model ID. If null, uses the default model.</param>
    /// <returns>An IChatClient configured for the specified model.</returns>
    IChatClient GetChatClient(string? modelId = null);
}
