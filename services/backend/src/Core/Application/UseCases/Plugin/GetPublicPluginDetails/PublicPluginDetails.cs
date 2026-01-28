namespace Application.UseCases.Plugin.GetPublicPluginDetails;

public sealed record PublicPluginDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    int PriceCredits,
    bool IsSubscriptionBased,
    bool IsActive,
    string? SourceUrl,
    string? SystemName,
    string? Author,
    string? Category,
    string? Icon,
    string? Tags,
    double Rating,
    int ReviewCount,
    int InstallCount,
    bool IsFeatured,
    string? Version,
    string? Compatibility,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt
);
