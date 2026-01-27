using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Port for OpenAPI spec operations.
/// </summary>
public interface IOpenApiSpecService
{
    /// <summary>
    /// Validates an OpenAPI spec at the given URL.
    /// </summary>
    Task<OpenApiSpecValidationResult> ValidateSpecAsync(
        string baseUrl,
        string? specPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the cached spec for a plugin.
    /// </summary>
    Task RefreshSpecAsync(PluginEntity plugin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the available functions from a plugin's spec.
    /// </summary>
    Task<IReadOnlyList<OpenApiFunction>> GetFunctionsAsync(
        PluginEntity plugin,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of validating an OpenAPI specification.
/// </summary>
public record OpenApiSpecValidationResult(
    bool IsValid,
    string? Title,
    string? Version,
    IReadOnlyList<OpenApiFunction> Operations,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Represents a single function/operation from an OpenAPI spec.
/// </summary>
public record OpenApiFunction(
    string Name,
    string? Description,
    string HttpMethod,
    string Path);
