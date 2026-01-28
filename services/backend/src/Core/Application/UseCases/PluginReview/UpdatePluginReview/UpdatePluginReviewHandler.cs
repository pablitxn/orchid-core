using Application.Interfaces;
using Application.UseCases.PluginReview.Common;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.PluginReview.UpdatePluginReview;

public sealed class UpdatePluginReviewHandler(
    IPluginRepository pluginRepository,
    IPluginReviewRepository reviewRepository,
    IUserRepository userRepository
) : IRequestHandler<UpdatePluginReviewCommand, PluginReviewDto>
{
    public async Task<PluginReviewDto> Handle(UpdatePluginReviewCommand request, CancellationToken ct)
    {
        UpdatePluginReviewCommandValidator.Validate(request);

        // Get review and verify ownership
        var review = await reviewRepository.GetByIdAsync(request.ReviewId, ct)
            ?? throw new PluginReviewNotFoundException(request.ReviewId);

        if (review.UserId != request.UserId)
        {
            throw new UnauthorizedDomainAccessException("PluginReview", request.ReviewId, request.UserId);
        }

        // Update fields
        review.Rating = request.Rating;
        review.Title = request.Title;
        review.Body = request.Body;
        review.UpdatedAt = DateTime.UtcNow;

        await reviewRepository.UpdateAsync(review, ct);

        // Recalculate plugin aggregates
        var plugin = await pluginRepository.GetByIdAsync(review.PluginId, ct);
        if (plugin is not null)
        {
            var newAverage = await reviewRepository.GetAverageRatingAsync(review.PluginId, ct);
            plugin.Rating = newAverage ?? 0.0;
            plugin.UpdatedAt = DateTime.UtcNow;
            await pluginRepository.UpdateAsync(plugin, ct);
        }

        var user = await userRepository.GetByIdAsync(request.UserId, ct);
        return MapToDto(review, GetDisplayName(user));
    }

    private static string GetDisplayName(UserEntity? user)
    {
        if (user?.Name is not null)
        {
            var parts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
                return $"{parts[0]} {parts[1][0]}.";
            return parts.Length > 0 ? parts[0] : "Anonymous";
        }
        return "Anonymous";
    }

    private static PluginReviewDto MapToDto(PluginReviewEntity entity, string userName) => new()
    {
        Id = entity.Id,
        PluginId = entity.PluginId,
        UserId = entity.UserId,
        UserName = userName,
        Rating = entity.Rating,
        Title = entity.Title,
        Body = entity.Body,
        HelpfulCount = entity.HelpfulCount,
        IsVerified = entity.IsVerified,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
