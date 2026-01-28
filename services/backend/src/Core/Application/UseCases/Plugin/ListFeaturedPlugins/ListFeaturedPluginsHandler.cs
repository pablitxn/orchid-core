using Application.Interfaces;
using Application.UseCases.Plugin.ListPublicPlugins;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Plugin.ListFeaturedPlugins;

public sealed class ListFeaturedPluginsHandler(
    IPluginRepository pluginRepository,
    ILogger<ListFeaturedPluginsHandler> logger
) : IRequestHandler<ListFeaturedPluginsQuery, ListFeaturedPluginsDto>
{
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly ILogger<ListFeaturedPluginsHandler> _logger = logger;

    public async Task<ListFeaturedPluginsDto> Handle(ListFeaturedPluginsQuery query, CancellationToken cancellationToken)
    {
        var allPlugins = await _pluginRepository.ListActiveAsync(cancellationToken);

        var featuredPlugins = allPlugins
            .Where(p => p.IsPublic && p.IsActive && p.IsFeatured && p.PublishedAt != null)
            .OrderByDescending(p => p.Rating)
            .ThenByDescending(p => p.InstallCount)
            .Take(query.Limit)
            .Select(p => new PublicPluginDto(
                p.Id,
                p.Name,
                p.Description,
                p.PriceCredits,
                p.IsSubscriptionBased,
                p.IsActive,
                p.SourceUrl,
                p.Author,
                p.Category,
                p.Icon,
                p.Tags,
                p.Rating,
                p.ReviewCount,
                p.InstallCount,
                p.IsFeatured,
                p.Version,
                p.Compatibility,
                p.CreatedAt,
                p.UpdatedAt,
                p.PublishedAt
            ))
            .ToList();

        _logger.LogInformation("Listed {Count} featured plugins", featuredPlugins.Count);

        return new ListFeaturedPluginsDto(featuredPlugins);
    }
}
