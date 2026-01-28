using Application.Interfaces;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.PluginReview.DeletePluginReview;

public sealed class DeletePluginReviewHandler(
    IPluginRepository pluginRepository,
    IPluginReviewRepository reviewRepository
) : IRequestHandler<DeletePluginReviewCommand, Unit>
{
    public async Task<Unit> Handle(DeletePluginReviewCommand request, CancellationToken ct)
    {
        // Get review and verify ownership
        var review = await reviewRepository.GetByIdAsync(request.ReviewId, ct)
            ?? throw new PluginReviewNotFoundException(request.ReviewId);

        if (review.UserId != request.UserId)
        {
            throw new UnauthorizedDomainAccessException("PluginReview", request.ReviewId, request.UserId);
        }

        var pluginId = review.PluginId;

        // Soft delete
        await reviewRepository.DeleteAsync(request.ReviewId, ct);

        // Recalculate plugin aggregates
        var plugin = await pluginRepository.GetByIdAsync(pluginId, ct);
        if (plugin is not null)
        {
            var newAverage = await reviewRepository.GetAverageRatingAsync(pluginId, ct);
            var reviewCount = await reviewRepository.GetReviewCountAsync(pluginId, ct);
            plugin.Rating = newAverage ?? 0.0;
            plugin.ReviewCount = reviewCount;
            plugin.UpdatedAt = DateTime.UtcNow;
            await pluginRepository.UpdateAsync(plugin, ct);
        }

        return Unit.Value;
    }
}
