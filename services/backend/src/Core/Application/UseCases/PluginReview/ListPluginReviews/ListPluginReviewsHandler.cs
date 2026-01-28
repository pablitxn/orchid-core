using Application.Common.Models;
using Application.Interfaces;
using Application.UseCases.PluginReview.Common;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.PluginReview.ListPluginReviews;

public sealed class ListPluginReviewsHandler(
    IPluginReviewRepository reviewRepository,
    IUserRepository userRepository
) : IRequestHandler<ListPluginReviewsQuery, ListPluginReviewsDto>
{
    public async Task<ListPluginReviewsDto> Handle(ListPluginReviewsQuery request, CancellationToken ct)
    {
        var sortByString = request.SortBy switch
        {
            PluginReviewSortBy.Oldest => "oldest",
            PluginReviewSortBy.Helpful => "helpful",
            PluginReviewSortBy.HighestRating => "highest",
            PluginReviewSortBy.LowestRating => "lowest",
            _ => "newest"
        };

        var result = await reviewRepository.ListByPluginAsync(
            request.PluginId,
            request.Page,
            request.PageSize,
            sortByString,
            ct);

        // Get all user IDs and batch load user names
        var userIds = result.Items.Select(r => r.UserId).Distinct().ToList();
        var users = new Dictionary<Guid, UserEntity>();

        foreach (var userId in userIds)
        {
            var user = await userRepository.GetByIdAsync(userId, ct);
            if (user is not null)
            {
                users[userId] = user;
            }
        }

        var reviewDtos = result.Items.Select(r => MapToDto(r, GetDisplayName(users.GetValueOrDefault(r.UserId)))).ToList();

        return new ListPluginReviewsDto(new PaginatedResult<PluginReviewDto>(
            reviewDtos,
            result.TotalCount,
            result.Page,
            result.PageSize));
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
