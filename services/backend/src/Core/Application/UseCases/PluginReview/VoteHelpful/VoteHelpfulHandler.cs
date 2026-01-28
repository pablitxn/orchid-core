using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.PluginReview.VoteHelpful;

public sealed class VoteHelpfulHandler(
    IPluginReviewRepository reviewRepository,
    IPluginReviewHelpfulVoteRepository voteRepository
) : IRequestHandler<VoteHelpfulCommand, VoteHelpfulDto>
{
    public async Task<VoteHelpfulDto> Handle(VoteHelpfulCommand request, CancellationToken ct)
    {
        // Verify review exists
        var review = await reviewRepository.GetByIdAsync(request.ReviewId, ct)
            ?? throw new PluginReviewNotFoundException(request.ReviewId);

        // Toggle vote
        var alreadyVoted = await voteRepository.ExistsAsync(request.ReviewId, request.UserId, ct);

        if (alreadyVoted)
        {
            // Remove vote
            await voteRepository.DeleteAsync(request.ReviewId, request.UserId, ct);
            review.HelpfulCount = Math.Max(0, review.HelpfulCount - 1);
        }
        else
        {
            // Add vote
            var vote = new PluginReviewHelpfulVoteEntity
            {
                Id = Guid.NewGuid(),
                ReviewId = request.ReviewId,
                UserId = request.UserId,
                CreatedAt = DateTime.UtcNow
            };
            await voteRepository.CreateAsync(vote, ct);
            review.HelpfulCount += 1;
        }

        review.UpdatedAt = DateTime.UtcNow;
        await reviewRepository.UpdateAsync(review, ct);

        return new VoteHelpfulDto(!alreadyVoted, review.HelpfulCount);
    }
}
