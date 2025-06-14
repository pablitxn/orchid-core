namespace Application.UseCases.Plugin.ListAvailablePlugins;

public sealed record PublicPluginDetails(
    Guid Id,
    string Name,
    string? Description,
    int PriceCredits,
    bool IsSubscriptionBased,
    bool IsActive,
    string? SourceUrl,
    string? SystemName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);