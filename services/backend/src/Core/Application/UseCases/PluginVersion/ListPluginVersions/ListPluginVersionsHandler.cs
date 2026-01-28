using Application.Interfaces;
using Application.UseCases.PluginVersion.Common;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.PluginVersion.ListPluginVersions;

public sealed class ListPluginVersionsHandler(
    IPluginVersionRepository pluginVersionRepository,
    IPluginRepository pluginRepository
) : IRequestHandler<ListPluginVersionsQuery, List<PluginVersionDto>>
{
    private readonly IPluginVersionRepository _pluginVersionRepository = pluginVersionRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;

    public async Task<List<PluginVersionDto>> Handle(ListPluginVersionsQuery request, CancellationToken cancellationToken)
    {
        // Verify plugin exists
        var plugin = await _pluginRepository.GetByIdAsync(request.PluginId, cancellationToken);
        if (plugin is null)
        {
            throw new PluginNotFoundException(request.PluginId);
        }

        var versions = await _pluginVersionRepository.ListByPluginAsync(request.PluginId, cancellationToken);

        return versions.Select(PluginVersionDto.FromEntity).ToList();
    }
}
