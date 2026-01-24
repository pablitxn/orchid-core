namespace Application.UseCases.Plugin.ListPublicPlugins;

public sealed record ListPublicPluginsDto(IEnumerable<PublicPluginDto> Plugins);

public sealed record PublicPluginDto(
    Guid Id,
    string Name,
    string? Description,
    int PriceCredits,
    bool IsSubscriptionBased,
    bool IsActive,
    string? SourceUrl,
    DateTime CreatedAt
);