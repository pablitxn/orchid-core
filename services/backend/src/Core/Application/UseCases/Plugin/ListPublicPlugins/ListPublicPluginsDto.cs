using Application.Common.Models;

namespace Application.UseCases.Plugin.ListPublicPlugins;

public sealed record ListPublicPluginsDto(PaginatedResult<PublicPluginDto> Plugins);

public sealed record PublicPluginDto(
    Guid Id,
    string Name,
    string? Description,
    int PriceCredits,
    bool IsSubscriptionBased,
    bool IsActive,
    string? SourceUrl,
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
