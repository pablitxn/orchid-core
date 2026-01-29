using Application.Interfaces;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.PluginAnalytics.GetPluginUsageStats;

public sealed class GetPluginUsageStatsHandler(
    IPluginUsageEventRepository usageRepository,
    IPluginRepository pluginRepository,
    IPluginReviewRepository reviewRepository,
    IPublisherRepository publisherRepository
) : IRequestHandler<GetPluginUsageStatsQuery, PluginUsageStatsDto>
{
    private readonly IPluginUsageEventRepository _usageRepository = usageRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly IPluginReviewRepository _reviewRepository = reviewRepository;
    private readonly IPublisherRepository _publisherRepository = publisherRepository;

    public async Task<PluginUsageStatsDto> Handle(GetPluginUsageStatsQuery request, CancellationToken cancellationToken)
    {
        // Verify the plugin exists
        var plugin = await _pluginRepository.GetByIdAsync(request.PluginId, cancellationToken)
            ?? throw new PluginNotFoundException(request.PluginId);

        // Verify the user is the publisher of this plugin (ownership check)
        var publisher = await _publisherRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (publisher == null || plugin.PublisherId != publisher.Id)
        {
            throw new UnauthorizedDomainAccessException("Plugin", request.PluginId, request.UserId);
        }

        // Calculate date range based on period
        var (startDate, endDate) = GetDateRange(request.Period);

        // Fetch usage stats
        var eventCounts = await _usageRepository.GetEventCountsByTypeAsync(
            request.PluginId, startDate, endDate, cancellationToken);

        var activeUsers = await _usageRepository.CountDistinctUsersAsync(
            request.PluginId, startDate, endDate, cancellationToken);

        var errorRate = await _usageRepository.GetErrorRateAsync(
            request.PluginId, startDate, endDate, cancellationToken);

        var dailyStats = await _usageRepository.GetDailyStatsAsync(
            request.PluginId, startDate, endDate, cancellationToken);

        // Fetch review stats
        var avgRating = await _reviewRepository.GetAverageRatingAsync(request.PluginId, cancellationToken);
        var reviewCount = await _reviewRepository.GetReviewCountAsync(request.PluginId, cancellationToken);

        return new PluginUsageStatsDto
        {
            PluginId = request.PluginId,
            Period = request.Period,
            Installs = eventCounts.GetValueOrDefault("installed", 0),
            Uninstalls = eventCounts.GetValueOrDefault("uninstalled", 0),
            ActiveUsers = activeUsers,
            Invocations = eventCounts.GetValueOrDefault("invoked", 0),
            ErrorRate = Math.Round(errorRate, 4),
            AvgRating = Math.Round(avgRating ?? 0.0, 1),
            ReviewCount = reviewCount,
            DailyBreakdown = dailyStats.Select(d => new DailyStatsDto
            {
                Date = d.Date,
                Invocations = d.Invocations,
                Errors = d.Errors,
                ActiveUsers = d.ActiveUsers
            }).ToList()
        };
    }

    private static (DateTime startDate, DateTime endDate) GetDateRange(string period)
    {
        var endDate = DateTime.UtcNow;
        var startDate = period switch
        {
            "last_7_days" => endDate.AddDays(-7),
            "last_30_days" => endDate.AddDays(-30),
            "last_90_days" => endDate.AddDays(-90),
            "all_time" => DateTime.MinValue,
            _ => endDate.AddDays(-30)
        };

        return (startDate, endDate);
    }
}
