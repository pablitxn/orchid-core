using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Plugin.ListAvailablePluginDtos;

public sealed class ListAvailablePluginDtosHandler(
    IPluginRepository pluginRepository,
    IUserPluginRepository userPluginRepository
) : IRequestHandler<ListAvailablePluginDtosQuery, ListAvailablePluginDtosDto>
{
    public async Task<ListAvailablePluginDtosDto> Handle(ListAvailablePluginDtosQuery query, CancellationToken cancellationToken)
    {
        var allPlugins = await pluginRepository.ListActiveAsync(cancellationToken);
        var userPlugins = await userPluginRepository.ListByUserAsync(query.UserId, cancellationToken);
        var userPluginIds = userPlugins.Where(up => up.IsActive).Select(up => up.PluginId).ToHashSet();

        var availablePlugins = allPlugins.Select(plugin => new AvailablePluginDto(
            Id: plugin.Id,
            Name: plugin.Name,
            Description: plugin.Description,
            PriceCredits: plugin.PriceCredits,
            IsSubscriptionBased: plugin.IsSubscriptionBased,
            UserOwns: userPluginIds.Contains(plugin.Id),
            IsActive: plugin.IsActive
        ));

        return new ListAvailablePluginDtosDto(availablePlugins);
    }
}