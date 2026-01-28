using Application.Interfaces;
using Application.UseCases.Publisher.Common;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Publisher.ListPublisherPlugins;

public class ListPublisherPluginsHandler(
    IPublisherRepository publisherRepository,
    IPluginRepository pluginRepository,
    IPluginSubmissionRepository submissionRepository
) : IRequestHandler<ListPublisherPluginsQuery, List<PublisherPluginDto>>
{
    private readonly IPublisherRepository _publisherRepository = publisherRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly IPluginSubmissionRepository _submissionRepository = submissionRepository;

    public async Task<List<PublisherPluginDto>> Handle(ListPublisherPluginsQuery request, CancellationToken cancellationToken)
    {
        var publisher = await _publisherRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (publisher is null)
        {
            throw new PublisherNotFoundException($"No publisher found for user '{request.UserId}'.");
        }

        // Get all plugins for this publisher
        var allPlugins = await _pluginRepository.ListAsync(cancellationToken);
        var publisherPlugins = allPlugins.Where(p => p.PublisherId == publisher.Id).ToList();

        // Get all submissions for this publisher to show pending/rejected status
        var submissions = await _submissionRepository.ListByPublisherAsync(publisher.Id, cancellationToken);
        var submissionsByPlugin = submissions
            .Where(s => s.PluginId.HasValue)
            .GroupBy(s => s.PluginId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First().Status);

        // Map to DTOs
        var result = publisherPlugins.Select(plugin =>
        {
            SubmissionStatus? latestStatus = submissionsByPlugin.TryGetValue(plugin.Id, out var status)
                ? status
                : null;
            return PublisherPluginDto.FromEntity(plugin, latestStatus);
        }).ToList();

        // Also include pending submissions that don't have a plugin yet
        var pendingSubmissions = submissions
            .Where(s => s.PluginId == null && s.Status == SubmissionStatus.Pending)
            .Select(s => new PublisherPluginDto
            {
                Id = Guid.Empty, // No plugin ID yet
                Name = s.Name,
                Description = s.ShortDescription,
                Category = s.Category,
                Version = s.Version,
                IsActive = false,
                IsPublic = false,
                Rating = 0,
                ReviewCount = 0,
                InstallCount = 0,
                PublishedAt = null,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                LatestSubmissionStatus = s.Status
            });

        result.AddRange(pendingSubmissions);

        return result.OrderByDescending(p => p.CreatedAt).ToList();
    }
}
