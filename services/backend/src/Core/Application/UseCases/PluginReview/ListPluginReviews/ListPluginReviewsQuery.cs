using Application.Common.Models;
using Application.UseCases.PluginReview.Common;
using MediatR;

namespace Application.UseCases.PluginReview.ListPluginReviews;

public sealed class ListPluginReviewsQuery : PaginatedQuery, IRequest<ListPluginReviewsDto>
{
    public Guid PluginId { get; set; }
    public PluginReviewSortBy SortBy { get; set; } = PluginReviewSortBy.Newest;
}
