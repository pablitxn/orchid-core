using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.OpenApiPlugin.ValidateOpenApiSpec;

public sealed class ValidateOpenApiSpecHandler(
    IOpenApiSpecService openApiSpecService,
    ILogger<ValidateOpenApiSpecHandler> logger) : IRequestHandler<ValidateOpenApiSpecQuery, OpenApiSpecValidationResultDto>
{
    public async Task<OpenApiSpecValidationResultDto> Handle(
        ValidateOpenApiSpecQuery request,
        CancellationToken cancellationToken)
    {
        OpenApiPluginValidator.ValidateBaseUrl(request.BaseUrl);
        OpenApiPluginValidator.ValidateOpenApiSpecUrl(request.OpenApiSpecUrl);

        logger.LogInformation("Validating OpenAPI spec at {BaseUrl}{SpecPath}",
            request.BaseUrl, request.OpenApiSpecUrl ?? "/openapi.json");

        var result = await openApiSpecService.ValidateSpecAsync(
            request.BaseUrl,
            request.OpenApiSpecUrl,
            cancellationToken);

        var operations = result.Operations.Select(op => new OpenApiFunctionDto(
            op.Name,
            op.Description,
            op.HttpMethod,
            op.Path
        )).ToList();

        return new OpenApiSpecValidationResultDto(
            result.IsValid,
            result.Title,
            result.Version,
            operations,
            result.Errors,
            result.Warnings);
    }
}
