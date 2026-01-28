using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Plugin.ListPluginCategories;

public sealed class ListPluginCategoriesHandler(
    IPluginRepository pluginRepository,
    ILogger<ListPluginCategoriesHandler> logger
) : IRequestHandler<ListPluginCategoriesQuery, ListPluginCategoriesDto>
{
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly ILogger<ListPluginCategoriesHandler> _logger = logger;

    public async Task<ListPluginCategoriesDto> Handle(ListPluginCategoriesQuery query, CancellationToken cancellationToken)
    {
        var allPlugins = await _pluginRepository.ListActiveAsync(cancellationToken);

        var categories = allPlugins
            .Where(p => p.IsPublic && p.IsActive && p.PublishedAt != null && !string.IsNullOrWhiteSpace(p.Category))
            .GroupBy(p => p.Category!)
            .Select(g => new PluginCategoryDto(g.Key, g.Count()))
            .OrderBy(c => c.Name)
            .ToList();

        _logger.LogInformation("Listed {Count} plugin categories", categories.Count);

        return new ListPluginCategoriesDto(categories);
    }
}
