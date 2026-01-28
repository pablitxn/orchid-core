using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Publisher.SubmitPlugin;

public class SubmitPluginHandler(
    IPublisherRepository publisherRepository,
    IPluginSubmissionRepository submissionRepository,
    IPluginRepository pluginRepository,
    IEventPublisher eventPublisher,
    ILogger<SubmitPluginHandler> logger
) : IRequestHandler<SubmitPluginCommand, SubmissionDto>
{
    private readonly IPublisherRepository _publisherRepository = publisherRepository;
    private readonly IPluginSubmissionRepository _submissionRepository = submissionRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly IEventPublisher _eventPublisher = eventPublisher;
    private readonly ILogger<SubmitPluginHandler> _logger = logger;

    public async Task<SubmissionDto> Handle(SubmitPluginCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Submitting plugin {PluginName} for user {UserId}", request.Name, request.UserId);

        // Get publisher for this user
        var publisher = await _publisherRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (publisher is null)
        {
            throw new PublisherNotFoundException($"No publisher found for user '{request.UserId}'. Register as a publisher first.");
        }

        // Check publisher is active
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new InvalidOperationException($"Publisher is not active. Current status: {publisher.Status}");
        }

        // Check if slug is already used by an existing plugin
        var existingPlugins = await _pluginRepository.ListAsync(cancellationToken);
        var slugExists = existingPlugins.Any(p =>
            p.SystemName?.Equals(request.Slug, StringComparison.OrdinalIgnoreCase) == true);
        if (slugExists)
        {
            throw new DuplicatePluginSlugException(request.Slug);
        }

        // Create submission
        var submission = new PluginSubmissionEntity
        {
            Id = Guid.NewGuid(),
            PublisherId = publisher.Id,
            PluginId = null,
            Name = request.Name,
            Slug = request.Slug,
            ShortDescription = request.ShortDescription,
            FullDescription = request.FullDescription,
            Category = request.Category,
            Tags = request.Tags,
            Version = request.Version,
            ManifestUrl = request.ManifestUrl,
            RepositoryUrl = request.RepositoryUrl,
            Compatibility = request.Compatibility,
            Status = SubmissionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _submissionRepository.CreateAsync(submission, cancellationToken);

        _logger.LogInformation("Plugin submission {SubmissionId} created for publisher {PublisherId}",
            submission.Id, publisher.Id);

        // Publish domain event
        await _eventPublisher.PublishAsync(
            new PluginSubmittedEvent(submission.Id, publisher.Id, submission.Name),
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
