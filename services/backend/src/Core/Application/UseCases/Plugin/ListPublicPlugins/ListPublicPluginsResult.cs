namespace Application.UseCases.Plugin.ListAvailablePlugins;

public sealed record ListPublicPluginsResult(IEnumerable<PublicPlugin> Plugins);

public sealed record PublicPlugin(
    Guid Id,
    string Name,
    string? Description,
    int PriceCredits,
    bool IsSubscriptionBased,
    bool IsActive,
    string? SourceUrl,
    DateTime CreatedAt
);