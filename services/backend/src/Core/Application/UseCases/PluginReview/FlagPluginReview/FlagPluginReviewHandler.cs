using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.PluginReview.FlagPluginReview;

public sealed class FlagPluginReviewHandler(
    IPluginReviewRepository reviewRepository,
    IPluginReviewFlagRepository flagRepository
) : IRequestHandler<FlagPluginReviewCommand, Unit>
{
    public async Task<Unit> Handle(FlagPluginReviewCommand request, CancellationToken ct)
    {
        // Verify review exists
        var review = await reviewRepository.GetByIdAsync(request.ReviewId, ct)
            ?? throw new PluginReviewNotFoundException(request.ReviewId);

        // Check if already flagged by this user
        var alreadyFlagged = await flagRepository.ExistsAsync(request.ReviewId, request.UserId, ct);
        if (alreadyFlagged)
        {
            throw new DuplicateReviewFlagException(request.UserId, request.ReviewId);
        }

        var flag = new PluginReviewFlagEntity
        {
            Id = Guid.NewGuid(),
            ReviewId = request.ReviewId,
            UserId = request.UserId,
            Reason = request.Reason,
            Details = request.Details,
            CreatedAt = DateTime.UtcNow
        };

        await flagRepository.CreateAsync(flag, ct);

        return Unit.Value;
    }
}
