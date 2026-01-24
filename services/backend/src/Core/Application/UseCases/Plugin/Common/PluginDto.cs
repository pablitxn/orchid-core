using Domain.Entities;

namespace Application.UseCases.Plugin.Common;

public sealed record PluginDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? SourceUrl { get; init; }
    public bool IsActive { get; init; }
    public int PriceCredits { get; init; }
    public bool IsSubscriptionBased { get; init; }
    public string? SystemName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static PluginDto FromEntity(PluginEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        SourceUrl = entity.SourceUrl,
        IsActive = entity.IsActive,
        PriceCredits = entity.PriceCredits,
        IsSubscriptionBased = entity.IsSubscriptionBased,
        SystemName = entity.SystemName,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
