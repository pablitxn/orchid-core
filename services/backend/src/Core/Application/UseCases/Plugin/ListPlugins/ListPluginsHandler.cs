using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Plugin.ListPlugins;

public class ListPluginsHandler(IPluginRepository repository) : IRequestHandler<ListPluginsQuery, List<PluginEntity>>
{
    private readonly IPluginRepository _repository = repository;

    public async Task<List<PluginEntity>> Handle(ListPluginsQuery request, CancellationToken cancellationToken)
    {
        return await _repository.ListAsync(cancellationToken);
    }
}