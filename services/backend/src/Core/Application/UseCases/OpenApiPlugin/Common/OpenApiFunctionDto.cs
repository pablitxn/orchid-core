namespace Application.UseCases.OpenApiPlugin.Common;

/// <summary>
/// DTO representing a single function/operation discovered from an OpenAPI spec.
/// </summary>
public sealed record OpenApiFunctionDto(
    string Name,
    string? Description,
    string HttpMethod,
    string Path);
