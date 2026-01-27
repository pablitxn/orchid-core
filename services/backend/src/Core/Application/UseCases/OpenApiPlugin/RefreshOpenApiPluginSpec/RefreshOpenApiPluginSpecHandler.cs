using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.Common;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.OpenApiPlugin.RefreshOpenApiPluginSpec;

public sealed class RefreshOpenApiPluginSpecHandler(
    IPluginRepository pluginRepository,
    IOpenApiSpecService openApiSpecService,
    ILogger<RefreshOpenApiPluginSpecHandler> logger) : IRequestHandler<RefreshOpenApiPluginSpecCommand, OpenApiPluginDto>
{
    public async Task<OpenApiPluginDto> Handle(
        RefreshOpenApiPluginSpecCommand request,
        CancellationToken cancellationToken)
    {
        var plugin = await pluginRepository.GetByIdAsync(request.Id, cancellationToken);

        if (plugin is null)
            throw new PluginNotFoundException(request.Id);

        if (plugin.PluginType != "openapi")
            throw new ValidationException(
                $"Plugin {request.Id} is not an OpenAPI plugin.",
                nameof(request.Id));

        logger.LogInformation(
            "Force refreshing OpenAPI spec for plugin: {PluginName} (Id: {PluginId})",
            plugin.Name,
            plugin.Id);

        await openApiSpecService.RefreshSpecAsync(plugin, cancellationToken);

        plugin = await pluginRepository.GetByIdAsync(request.Id, cancellationToken);

        var functions = await GetAvailableFunctionsAsync(plugin!, cancellationToken);

        logger.LogInformation(
            "Successfully refreshed OpenAPI spec for plugin: {PluginName}, found {FunctionCount} functions",
            plugin!.Name,
            functions.Count);

        return OpenApiPluginDto.FromEntity(plugin, functions);
    }

    private async Task<IReadOnlyList<OpenApiFunctionDto>> GetAvailableFunctionsAsync(
        Domain.Entities.PluginEntity plugin,
        CancellationToken cancellationToken)
    {
        try
        {
            var apiFunctions = await openApiSpecService.GetFunctionsAsync(plugin, cancellationToken);

            return apiFunctions.Select(f => new OpenApiFunctionDto(
                f.Name,
                f.Description,
                f.HttpMethod,
                f.Path
            )).ToList();
        }
        catch
        {
            return [];
        }
    }
}
