using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.AgentFramework.Abstractions;

/// <summary>
/// Central registry for all available tools in the Agent Framework.
/// Provides lookup by system name and tool discovery.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets a tool by its system name.
    /// </summary>
    /// <param name="systemName">The system name of the tool (e.g., "vector_store", "web_search").</param>
    /// <returns>The tool if found, null otherwise.</returns>
    ITool? GetToolBySystemName(string systemName);

    /// <summary>
    /// Gets the AIFunctions for a tool by its system name.
    /// </summary>
    /// <param name="systemName">The system name of the tool.</param>
    /// <returns>List of AIFunction instances, or empty list if tool not found.</returns>
    IReadOnlyList<AIFunction> GetAIFunctionsBySystemName(string systemName);

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    /// <returns>Collection of all available tools.</returns>
    IReadOnlyCollection<ITool> GetAllTools();

    /// <summary>
    /// Checks if a tool with the given system name is registered.
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the tool is registered, false otherwise.</returns>
    bool IsToolRegistered(string systemName);
}
