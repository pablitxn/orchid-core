using Application.Interfaces;
using Application.UseCases.Publisher.Common;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Publisher.GetPluginStats;

public class GetPluginStatsHandler(
    IPublisherRepository publisherRepository,
    IPluginRepository pluginRepository,
    IUserPluginRepository userPluginRepository,
    IPluginReviewRepository pluginReviewRepository
) : IRequestHandler<GetPluginStatsQuery, PluginStatsDto>
{
    private readonly IPublisherRepository _publisherRepository = publisherRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly IUserPluginRepository _userPluginRepository = userPluginRepository;
    private readonly IPluginReviewRepository _pluginReviewRepository = pluginReviewRepository;

    public async Task<PluginStatsDto> Handle(GetPluginStatsQuery request, CancellationToken cancellationToken)
    {
        // Get publisher for this user
        var publisher = await _publisherRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (publisher is null)
        {
            throw new PublisherNotFoundException($"No publisher found for user '{request.UserId}'.");
        }

        // Get the plugin
        var plugin = await _pluginRepository.GetByIdAsync(request.PluginId, cancellationToken);
        if (plugin is null)
        {
            throw new PluginNotFoundException(request.PluginId);
        }

        // Verify ownership
        if (plugin.PublisherId != publisher.Id)
        {
            throw new UnauthorizedDomainAccessException("Plugin", request.PluginId, request.UserId);
        }

        // Get stats
        var avgRating = await _pluginReviewRepository.GetAverageRatingAsync(request.PluginId, cancellationToken);
        var reviewCount = await _pluginReviewRepository.GetReviewCountAsync(request.PluginId, cancellationToken);

        return new PluginStatsDto
        {
            PluginId = plugin.Id,
            PluginName = plugin.Name,
            TotalInstalls = plugin.InstallCount,
            AverageRating = avgRating ?? 0,
            TotalReviews = reviewCount,
            ActiveUsers = plugin.InstallCount, // Simplified - would need UserPlugin active count query
            LastInstallAt = null // Would need a query to get this
        };
    }
}
