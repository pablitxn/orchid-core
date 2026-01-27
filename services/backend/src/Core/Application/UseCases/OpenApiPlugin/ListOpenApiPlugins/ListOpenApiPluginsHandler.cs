using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.Common;
using MediatR;

namespace Application.UseCases.OpenApiPlugin.ListOpenApiPlugins;

public sealed class ListOpenApiPluginsHandler(
    IPluginRepository pluginRepository) : IRequestHandler<ListOpenApiPluginsQuery, IReadOnlyList<OpenApiPluginDto>>
{
    public async Task<IReadOnlyList<OpenApiPluginDto>> Handle(
        ListOpenApiPluginsQuery request,
        CancellationToken cancellationToken)
    {
        var allPlugins = await pluginRepository.ListAsync(cancellationToken);

        return allPlugins
            .Where(p => p.PluginType == "openapi")
            .Select(p => OpenApiPluginDto.FromEntity(p))
            .ToList();
    }
}
