using Domain.Entities;
using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.AgentFramework.Abstractions;

/// <summary>
/// Factory for creating AI functions from OpenAPI plugin entities.
/// Each operation in the OpenAPI spec becomes an AIFunction that the AI agent can invoke.
/// </summary>
public interface IOpenApiToolFactory
{
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
