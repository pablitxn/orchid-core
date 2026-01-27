using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.Common;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.OpenApiPlugin.GetOpenApiPlugin;

public sealed class GetOpenApiPluginHandler(
    IPluginRepository pluginRepository,
    IOpenApiSpecService openApiSpecService,
    ILogger<GetOpenApiPluginHandler> logger) : IRequestHandler<GetOpenApiPluginQuery, OpenApiPluginDto>
{
    public async Task<OpenApiPluginDto> Handle(
        GetOpenApiPluginQuery request,
        CancellationToken cancellationToken)
    {
        var plugin = await pluginRepository.GetByIdAsync(request.Id, cancellationToken);

        if (plugin is null)
            throw new PluginNotFoundException(request.Id);

        if (plugin.PluginType != "openapi")
            throw new ValidationException(
                $"Plugin {request.Id} is not an OpenAPI plugin.",
                nameof(request.Id));

        var functions = await GetAvailableFunctionsAsync(plugin, cancellationToken);

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
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load functions for OpenAPI plugin {PluginId}", plugin.Id);
            return [];
        }
    }
}
