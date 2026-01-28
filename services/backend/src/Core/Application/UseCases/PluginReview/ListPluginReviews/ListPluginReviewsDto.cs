using Application.Common.Models;
using Application.UseCases.PluginReview.Common;

namespace Application.UseCases.PluginReview.ListPluginReviews;

public sealed record ListPluginReviewsDto(PaginatedResult<PluginReviewDto> Reviews);
