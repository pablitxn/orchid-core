using Application.Interfaces;
using Application.UseCases.Plugin.Common;
using MediatR;

namespace Application.UseCases.Plugin.ListPlugins;

public class ListPluginsHandler(IPluginRepository repository) : IRequestHandler<ListPluginsQuery, List<PluginDto>>
{
    private readonly IPluginRepository _repository = repository;

    public async Task<List<PluginDto>> Handle(ListPluginsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListAsync(cancellationToken);
        return entities.Select(PluginDto.FromEntity).ToList();
    }
}
