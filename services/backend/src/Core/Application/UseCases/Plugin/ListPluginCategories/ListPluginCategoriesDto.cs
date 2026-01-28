namespace Application.UseCases.Plugin.ListPluginCategories;

public sealed record ListPluginCategoriesDto(IReadOnlyList<PluginCategoryDto> Categories);

public sealed record PluginCategoryDto(string Name, int PluginCount);
