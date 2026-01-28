using Application.UseCases.Plugin.ListPublicPlugins;

namespace Application.UseCases.Plugin.ListFeaturedPlugins;

public sealed record ListFeaturedPluginsDto(IReadOnlyList<PublicPluginDto> Plugins);
