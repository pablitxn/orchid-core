using Application.Common.Models;
using MediatR;

namespace Application.UseCases.Plugin.ListPublicPlugins;

public sealed class ListPublicPluginsQuery : PaginatedQuery, IRequest<ListPublicPluginsDto>
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public decimal? MinRating { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Compatibility { get; set; }
    public PluginSortBy SortBy { get; set; } = PluginSortBy.Rating;
    public bool SortDescending { get; set; } = true;
}
