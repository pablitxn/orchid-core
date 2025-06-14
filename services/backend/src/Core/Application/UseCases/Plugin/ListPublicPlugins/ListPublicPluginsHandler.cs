using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Plugin.ListAvailablePlugins;

public sealed class ListPublicPluginsHandler(
    IPluginRepository pluginRepository
) : IRequestHandler<ListPublicPluginsQuery, ListPublicPluginsResult>
{
    public async Task<ListPublicPluginsResult> Handle(ListPublicPluginsQuery query, CancellationToken cancellationToken)
    {
        var allPlugins = await pluginRepository.ListActiveAsync(cancellationToken);

        var publicPlugins = allPlugins.Select(plugin => new PublicPlugin(
            Id: plugin.Id,
            Name: plugin.Name,
            Description: plugin.Description,
            PriceCredits: plugin.PriceCredits,
            IsSubscriptionBased: plugin.IsSubscriptionBased,
            IsActive: plugin.IsActive,
            SourceUrl: plugin.SourceUrl,
            CreatedAt: plugin.CreatedAt
        ));

        return new ListPublicPluginsResult(publicPlugins);
    }
}