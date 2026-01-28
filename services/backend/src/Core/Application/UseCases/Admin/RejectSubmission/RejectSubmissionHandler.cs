using Application.Interfaces;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Admin.RejectSubmission;

public class RejectSubmissionHandler(
    IPluginSubmissionRepository submissionRepository,
    IEventPublisher eventPublisher,
    ILogger<RejectSubmissionHandler> logger
) : IRequestHandler<RejectSubmissionCommand, RejectSubmissionResult>
{
    private readonly IPluginSubmissionRepository _submissionRepository = submissionRepository;
    private readonly IEventPublisher _eventPublisher = eventPublisher;
    private readonly ILogger<RejectSubmissionHandler> _logger = logger;

    public async Task<RejectSubmissionResult> Handle(RejectSubmissionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rejecting submission {SubmissionId} by admin {ReviewedBy}",
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

        // Update submission status
        submission.Status = SubmissionStatus.Rejected;
        submission.ReviewedBy = request.ReviewedBy;
        submission.ReviewNotes = request.Reason;
        submission.ReviewedAt = DateTime.UtcNow;

        await _submissionRepository.UpdateAsync(submission, cancellationToken);

        _logger.LogInformation("Submission {SubmissionId} rejected. Reason: {Reason}",
            submission.Id, request.Reason);

        // Publish domain event
        await _eventPublisher.PublishAsync(
            new PluginRejectedEvent(submission.Id, request.ReviewedBy, request.Reason),
            cancellationToken);

        return new RejectSubmissionResult
        {
            SubmissionId = submission.Id,
            PluginName = submission.Name,
            Reason = request.Reason
        };
    }
}
