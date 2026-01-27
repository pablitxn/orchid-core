using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.AgentFramework.Abstractions;

/// <summary>
/// Central registry for all available tools in the Agent Framework.
/// Provides lookup by system name and tool discovery.
/// Supports both builtin tools (sync) and OpenAPI tools (async).
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets a builtin tool by its system name (sync, fast path).
    /// Only searches builtin tools registered in the registry.
    /// </summary>
    /// <param name="systemName">The system name of the tool (e.g., "vector_store", "web_search").</param>
    /// <returns>The tool if found, null otherwise.</returns>
    ITool? GetToolBySystemName(string systemName);

    /// <summary>
    /// Gets a tool by its system name, supporting both builtin and OpenAPI tools (async).
    /// First checks builtin tools (fast path), then queries the database for OpenAPI plugins.
    /// </summary>
    /// <param name="systemName">The system name of the tool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tool if found, null otherwise.</returns>
    Task<ITool?> GetToolBySystemNameAsync(string systemName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the AIFunctions for a tool by its system name (sync, builtin tools only).
    /// </summary>
    /// <param name="systemName">The system name of the tool.</param>
    /// <returns>List of AIFunction instances, or empty list if tool not found.</returns>
    IReadOnlyList<AIFunction> GetAIFunctionsBySystemName(string systemName);

    /// <summary>
    /// Gets the AIFunctions for a tool by its system name, supporting both builtin and OpenAPI tools.
    /// </summary>
    /// <param name="systemName">The system name of the tool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of AIFunction instances, or empty list if tool not found.</returns>
    Task<IReadOnlyList<AIFunction>> GetAIFunctionsBySystemNameAsync(string systemName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registered builtin tools.
    /// Does not include OpenAPI tools as they are resolved dynamically.
    /// </summary>
    /// <returns>Collection of all available builtin tools.</returns>
    IReadOnlyCollection<ITool> GetAllTools();

    /// <summary>
    /// Checks if a builtin tool with the given system name is registered.
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the tool is registered, false otherwise.</returns>
    bool IsToolRegistered(string systemName);

    /// <summary>
    /// Checks if a tool with the given system name exists (builtin or OpenAPI).
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the tool exists, false otherwise.</returns>
    Task<bool> IsToolRegisteredAsync(string systemName, CancellationToken cancellationToken = default);
}
