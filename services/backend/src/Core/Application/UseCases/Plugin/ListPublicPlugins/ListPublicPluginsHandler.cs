using Application.Common.Models;
using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Plugin.ListPublicPlugins;

public sealed class ListPublicPluginsHandler(
    IPluginRepository pluginRepository,
    ILogger<ListPublicPluginsHandler> logger
) : IRequestHandler<ListPublicPluginsQuery, ListPublicPluginsDto>
{
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly ILogger<ListPublicPluginsHandler> _logger = logger;

    public async Task<ListPublicPluginsDto> Handle(ListPublicPluginsQuery query, CancellationToken cancellationToken)
    {
        var allPlugins = await _pluginRepository.ListActiveAsync(cancellationToken);

        // Filter only public, active plugins with published date
        var filteredPlugins = allPlugins
            .Where(p => p.IsPublic && p.IsActive && p.PublishedAt != null)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchLower = query.SearchTerm.ToLowerInvariant();
            filteredPlugins = filteredPlugins.Where(p =>
                (p.Name != null && p.Name.ToLowerInvariant().Contains(searchLower)) ||
                (p.Description != null && p.Description.ToLowerInvariant().Contains(searchLower)) ||
                (p.Tags != null && p.Tags.ToLowerInvariant().Contains(searchLower)));
        }

        // Apply category filter
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            filteredPlugins = filteredPlugins.Where(p =>
                p.Category != null && p.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase));
        }

        // Apply rating filter
        if (query.MinRating.HasValue)
        {
            filteredPlugins = filteredPlugins.Where(p => p.Rating >= (double)query.MinRating.Value);
        }

        // Apply price filters
        if (query.MinPrice.HasValue)
        {
            filteredPlugins = filteredPlugins.Where(p => p.PriceCredits >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            filteredPlugins = filteredPlugins.Where(p => p.PriceCredits <= query.MaxPrice.Value);
        }

        // Apply compatibility filter
        if (!string.IsNullOrWhiteSpace(query.Compatibility))
        {
            filteredPlugins = filteredPlugins.Where(p =>
                p.Compatibility == null ||
                p.Compatibility == "all" ||
                p.Compatibility.Contains(query.Compatibility, StringComparison.OrdinalIgnoreCase));
        }

        // Count before pagination
        var totalCount = filteredPlugins.Count();

        // Apply sorting
        filteredPlugins = ApplySorting(filteredPlugins, query.SortBy, query.SortDescending);

        // Apply pagination
        var paginatedPlugins = filteredPlugins
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(p => new PublicPluginDto(
                p.Id,
                p.Name,
                p.Description,
                p.PriceCredits,
                p.IsSubscriptionBased,
                p.IsActive,
                p.SourceUrl,
                p.Author,
                p.Category,
                p.Icon,
                p.Tags,
                p.Rating,
                p.ReviewCount,
                p.InstallCount,
                p.IsFeatured,
                p.Version,
                p.Compatibility,
                p.CreatedAt,
                p.UpdatedAt,
                p.PublishedAt
            ))
            .ToList();

        var paginatedResult = new PaginatedResult<PublicPluginDto>(
            paginatedPlugins,
            totalCount,
            query.Page,
            query.PageSize);

        _logger.LogInformation("Listed {Count} public plugins out of {Total} (page {Page})",
            paginatedPlugins.Count, totalCount, query.Page);

        return new ListPublicPluginsDto(paginatedResult);
    }

    private static IQueryable<Domain.Entities.PluginEntity> ApplySorting(
        IQueryable<Domain.Entities.PluginEntity> query,
        PluginSortBy sortBy,
        bool descending)
    {
        return sortBy switch
        {
            PluginSortBy.Name => descending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),

            PluginSortBy.Price => descending
                ? query.OrderByDescending(p => p.PriceCredits)
                : query.OrderBy(p => p.PriceCredits),

            PluginSortBy.Rating => descending
                ? query.OrderByDescending(p => p.Rating).ThenByDescending(p => p.ReviewCount)
                : query.OrderBy(p => p.Rating).ThenBy(p => p.ReviewCount),

            PluginSortBy.Popularity => descending
                ? query.OrderByDescending(p => p.InstallCount)
                : query.OrderBy(p => p.InstallCount),

            PluginSortBy.Newest => descending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),

            PluginSortBy.Featured => query
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.Rating),

            _ => query.OrderByDescending(p => p.Rating)
        };
    }
}
