using MediatR;

namespace Application.UseCases.OpenApiPlugin.ValidateOpenApiSpec;

/// <summary>
/// Query to validate an OpenAPI spec URL without creating a plugin.
/// </summary>
public sealed record ValidateOpenApiSpecQuery(
    string BaseUrl,
    string? OpenApiSpecUrl = null) : IRequest<OpenApiSpecValidationResultDto>;
