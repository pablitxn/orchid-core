using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Plugin.ListAvailablePlugins;

public sealed class GetPublicPluginDetailsHandler(
    IPluginRepository pluginRepository
) : IRequestHandler<GetPublicPluginDetailsQuery, PublicPluginDetailsDto?>
{
    public async Task<PublicPluginDetailsDto?> Handle(GetPublicPluginDetailsQuery query, CancellationToken cancellationToken)
    {
        var plugin = await pluginRepository.GetByIdAsync(query.PluginId, cancellationToken);
        
        if (plugin == null || !plugin.IsActive)
        {
            return null;
        }

        return new PublicPluginDetailsDto(
            Id: plugin.Id,
            Name: plugin.Name,
            Description: plugin.Description,
            PriceCredits: plugin.PriceCredits,
            IsSubscriptionBased: plugin.IsSubscriptionBased,
            IsActive: plugin.IsActive,
            SourceUrl: plugin.SourceUrl,
            SystemName: plugin.SystemName,
            CreatedAt: plugin.CreatedAt,
            UpdatedAt: plugin.UpdatedAt
        );
    }
}