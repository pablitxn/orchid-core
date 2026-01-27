using Application.UseCases.OpenApiPlugin.Common;

namespace Application.UseCases.OpenApiPlugin.ValidateOpenApiSpec;

/// <summary>
/// Result of validating an OpenAPI specification.
/// </summary>
public sealed record OpenApiSpecValidationResultDto(
    bool IsValid,
    string? Title,
    string? Version,
    IReadOnlyList<OpenApiFunctionDto> DiscoveredOperations,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
