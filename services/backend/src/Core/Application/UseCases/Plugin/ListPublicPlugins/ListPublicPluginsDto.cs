namespace Application.UseCases.Plugin.ListAvailablePlugins;

public sealed record ListPublicPluginDtosDto(IEnumerable<PublicPluginDto> Plugins);

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