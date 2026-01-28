using Application.Interfaces;
using Application.UseCases.PluginReview.Common;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.PluginReview.CreatePluginReview;

public sealed class CreatePluginReviewHandler(
    IPluginRepository pluginRepository,
    IUserPluginRepository userPluginRepository,
    IPluginReviewRepository reviewRepository,
    IUserRepository userRepository
) : IRequestHandler<CreatePluginReviewCommand, PluginReviewDto>
{
    public async Task<PluginReviewDto> Handle(CreatePluginReviewCommand request, CancellationToken ct)
    {
        CreatePluginReviewCommandValidator.Validate(request);

        // Verify plugin exists
        var plugin = await pluginRepository.GetByIdAsync(request.PluginId, ct)
            ?? throw new PluginNotFoundException(request.PluginId);

        // Verify user owns plugin
        var userPlugin = await userPluginRepository.GetByUserAndPluginAsync(request.UserId, request.PluginId, ct)
            ?? throw new PluginNotOwnedException(request.UserId, request.PluginId);

        // Check if user already reviewed
        var existingReview = await reviewRepository.GetByPluginAndUserAsync(request.PluginId, request.UserId, ct);
        if (existingReview is not null)
        {
            throw new DuplicateReviewException(request.UserId, request.PluginId);
        }

        // Calculate verified status (owned 24h+)
        var isVerified = userPlugin.PurchasedAt <= DateTime.UtcNow.AddHours(-24);

        var review = new PluginReviewEntity
        {
            Id = Guid.NewGuid(),
            PluginId = request.PluginId,
            UserId = request.UserId,
            Rating = request.Rating,
            Title = request.Title,
            Body = request.Body,
            IsVerified = isVerified,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await reviewRepository.CreateAsync(review, ct);

        // Update denormalized rating on PluginEntity
        var newAverage = await reviewRepository.GetAverageRatingAsync(request.PluginId, ct);
        var reviewCount = await reviewRepository.GetReviewCountAsync(request.PluginId, ct);
        plugin.Rating = newAverage ?? 0.0;
        plugin.ReviewCount = reviewCount;
        plugin.UpdatedAt = DateTime.UtcNow;
        await pluginRepository.UpdateAsync(plugin, ct);

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
