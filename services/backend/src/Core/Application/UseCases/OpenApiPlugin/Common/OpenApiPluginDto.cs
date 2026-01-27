using Domain.Entities;

namespace Application.UseCases.OpenApiPlugin.Common;

/// <summary>
/// DTO representing an OpenAPI plugin with its configuration and available functions.
/// </summary>
public sealed record OpenApiPluginDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string SystemName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string? OpenApiSpecUrl { get; init; }
    public bool IsActive { get; init; }
    public int PriceCredits { get; init; }
    public bool IsSubscriptionBased { get; init; }
    public int TimeoutSeconds { get; init; }
    public DateTime? SpecCachedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<OpenApiFunctionDto> AvailableFunctions { get; init; } = [];

    public static OpenApiPluginDto FromEntity(
        PluginEntity entity,
        IReadOnlyList<OpenApiFunctionDto>? functions = null) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        SystemName = entity.SystemName ?? string.Empty,
        BaseUrl = entity.BaseUrl ?? string.Empty,
        OpenApiSpecUrl = entity.OpenApiSpecUrl,
        IsActive = entity.IsActive,
        PriceCredits = entity.PriceCredits,
        IsSubscriptionBased = entity.IsSubscriptionBased,
        TimeoutSeconds = entity.TimeoutSeconds ?? 30,
        SpecCachedAt = entity.SpecCachedAt,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        AvailableFunctions = functions ?? []
    };
}
