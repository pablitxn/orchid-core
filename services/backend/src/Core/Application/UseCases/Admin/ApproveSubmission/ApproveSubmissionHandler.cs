using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Admin.ApproveSubmission;

public class ApproveSubmissionHandler(
    IPluginSubmissionRepository submissionRepository,
    IPluginRepository pluginRepository,
    IPublisherRepository publisherRepository,
    IEventPublisher eventPublisher,
    ILogger<ApproveSubmissionHandler> logger
) : IRequestHandler<ApproveSubmissionCommand, ApproveSubmissionResult>
{
    private readonly IPluginSubmissionRepository _submissionRepository = submissionRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly IPublisherRepository _publisherRepository = publisherRepository;
    private readonly IEventPublisher _eventPublisher = eventPublisher;
    private readonly ILogger<ApproveSubmissionHandler> _logger = logger;

    public async Task<ApproveSubmissionResult> Handle(ApproveSubmissionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Approving submission {SubmissionId} by admin {ReviewedBy}",
            request.SubmissionId, request.ReviewedBy);

        // Get the submission
        var submission = await _submissionRepository.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission is null)
        {
            throw new SubmissionNotFoundException(request.SubmissionId);
        }

        // Verify submission is pending
        if (submission.Status != SubmissionStatus.Pending)
        {
            throw new InvalidSubmissionStatusException(
                request.SubmissionId, submission.Status, SubmissionStatus.Pending);
        }

        bool isNewPlugin = submission.PluginId is null;
        PluginEntity plugin;

        if (isNewPlugin)
        {
            // Create new plugin from submission
            plugin = new PluginEntity
            {
                Id = Guid.NewGuid(),
                Name = submission.Name,
                SystemName = submission.Slug,
                Description = submission.ShortDescription,
                Category = submission.Category,
                Tags = submission.Tags,
                Version = submission.Version,
                SourceUrl = submission.RepositoryUrl,
                Compatibility = submission.Compatibility,
                PublisherId = submission.PublisherId,
                Author = submission.Publisher?.Name,
                PluginType = "openapi", // Default to openapi for marketplace plugins
                IsActive = true,
                IsPublic = true,
                PublishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _pluginRepository.CreateAsync(plugin, cancellationToken);
            submission.PluginId = plugin.Id;

            _logger.LogInformation("Created new plugin {PluginId} from submission {SubmissionId}",
                plugin.Id, submission.Id);
        }
        else
        {
            // Update existing plugin
            plugin = await _pluginRepository.GetByIdAsync(submission.PluginId!.Value, cancellationToken)
                ?? throw new PluginNotFoundException(submission.PluginId!.Value);

            plugin.Name = submission.Name;
            plugin.Description = submission.ShortDescription;
            plugin.Category = submission.Category;
            plugin.Tags = submission.Tags;
            plugin.Version = submission.Version;
            plugin.SourceUrl = submission.RepositoryUrl;
            plugin.Compatibility = submission.Compatibility;
            plugin.UpdatedAt = DateTime.UtcNow;

            await _pluginRepository.UpdateAsync(plugin, cancellationToken);

            _logger.LogInformation("Updated plugin {PluginId} from submission {SubmissionId}",
                plugin.Id, submission.Id);
        }

        // Update submission status
        submission.Status = SubmissionStatus.Approved;
        submission.ReviewedBy = request.ReviewedBy;
        submission.ReviewNotes = request.ReviewNotes;
        submission.ReviewedAt = DateTime.UtcNow;

        await _submissionRepository.UpdateAsync(submission, cancellationToken);

        // Publish domain event
        await _eventPublisher.PublishAsync(
            new PluginApprovedEvent(plugin.Id, submission.Id, request.ReviewedBy),
            cancellationToken);

        return new ApproveSubmissionResult
        {
            SubmissionId = submission.Id,
            PluginId = plugin.Id,
            PluginName = plugin.Name,
            IsNewPlugin = isNewPlugin
        };
    }
}
