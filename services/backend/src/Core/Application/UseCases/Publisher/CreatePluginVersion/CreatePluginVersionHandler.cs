using Application.Interfaces;
using Application.UseCases.Publisher.SubmitPlugin;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Publisher.CreatePluginVersion;

public class CreatePluginVersionHandler(
    IPublisherRepository publisherRepository,
    IPluginRepository pluginRepository,
    IPluginSubmissionRepository submissionRepository,
    IEventPublisher eventPublisher,
    ILogger<CreatePluginVersionHandler> logger
) : IRequestHandler<CreatePluginVersionCommand, SubmissionDto>
{
    private readonly IPublisherRepository _publisherRepository = publisherRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly IPluginSubmissionRepository _submissionRepository = submissionRepository;
    private readonly IEventPublisher _eventPublisher = eventPublisher;
    private readonly ILogger<CreatePluginVersionHandler> _logger = logger;

    public async Task<SubmissionDto> Handle(CreatePluginVersionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating version {Version} for plugin {PluginId}", request.Version, request.PluginId);

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

        // Create a new submission for the version (requires review)
        var submission = new PluginSubmissionEntity
        {
            Id = Guid.NewGuid(),
            PublisherId = publisher.Id,
            PluginId = plugin.Id,
            Name = plugin.Name,
            Slug = plugin.SystemName ?? plugin.Name.ToLowerInvariant().Replace(" ", "-"),
            ShortDescription = plugin.Description ?? string.Empty,
            FullDescription = request.ReleaseNotes,
            Category = plugin.Category ?? string.Empty,
            Tags = plugin.Tags,
            Version = request.Version,
            ManifestUrl = request.ManifestUrl,
            RepositoryUrl = plugin.SourceUrl,
            Compatibility = request.Compatibility ?? plugin.Compatibility,
            Status = SubmissionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _submissionRepository.CreateAsync(submission, cancellationToken);

        _logger.LogInformation("Plugin version submission {SubmissionId} created for plugin {PluginId} version {Version}",
            submission.Id, plugin.Id, request.Version);

        // Publish domain event
        await _eventPublisher.PublishAsync(
            new PluginSubmittedEvent(submission.Id, publisher.Id, $"{submission.Name} v{request.Version}"),
            cancellationToken);

        return ToDto(submission);
    }

    private static SubmissionDto ToDto(PluginSubmissionEntity entity) => new()
    {
        Id = entity.Id,
        PublisherId = entity.PublisherId,
        PluginId = entity.PluginId,
        Name = entity.Name,
        Slug = entity.Slug,
        ShortDescription = entity.ShortDescription,
        FullDescription = entity.FullDescription,
        Category = entity.Category,
        Tags = entity.Tags,
        Version = entity.Version,
        ManifestUrl = entity.ManifestUrl,
        RepositoryUrl = entity.RepositoryUrl,
        Compatibility = entity.Compatibility,
        Status = entity.Status.ToString(),
        ReviewNotes = entity.ReviewNotes,
        ReviewedAt = entity.ReviewedAt,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
