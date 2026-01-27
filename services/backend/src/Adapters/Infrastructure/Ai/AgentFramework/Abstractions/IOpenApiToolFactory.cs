using Domain.Entities;
using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.AgentFramework.Abstractions;

/// <summary>
/// Factory for creating AI functions from OpenAPI plugin entities.
/// Each operation in the OpenAPI spec becomes an AIFunction that the AI agent can invoke.
/// Handles spec fetching, caching, and cache invalidation.
/// </summary>
public interface IOpenApiToolFactory
{
    /// <summary>
    /// Determines if this factory can handle the given plugin.
    /// </summary>
    /// <param name="plugin">The plugin entity to check.</param>
    /// <returns>True if the plugin is an OpenAPI plugin with a valid BaseUrl.</returns>
    bool CanHandle(PluginEntity plugin);

    /// <summary>
    /// Creates an ITool instance from an OpenAPI plugin entity.
    /// Fetches and parses the OpenAPI spec (using cache when valid),
    /// then generates an OpenApiTool with AIFunctions for each operation.
    /// </summary>
    /// <param name="plugin">The plugin entity containing OpenAPI configuration (BaseUrl, OpenApiSpecUrl, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An ITool instance, or null if the spec cannot be fetched/parsed.</returns>
    Task<ITool?> CreateToolAsync(PluginEntity plugin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a refresh of the cached OpenAPI spec for the given plugin.
    /// Fetches the spec from the remote URL and updates the cache regardless of TTL.
    /// </summary>
    /// <param name="plugin">The plugin entity to refresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task RefreshSpecAsync(PluginEntity plugin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates AI functions from an OpenAPI plugin entity.
    /// Fetches and parses the OpenAPI spec, then generates AIFunction instances
    /// for each operation defined in the spec.
    /// </summary>
    /// <param name="plugin">The plugin entity containing OpenAPI configuration (BaseUrl, OpenApiSpecUrl, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of AIFunction instances generated from the OpenAPI spec.</returns>
    Task<IReadOnlyList<AIFunction>> CreateFunctionsAsync(PluginEntity plugin, CancellationToken cancellationToken = default);
}
