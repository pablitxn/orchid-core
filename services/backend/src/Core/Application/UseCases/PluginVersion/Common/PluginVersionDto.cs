using Domain.Entities;

namespace Application.UseCases.PluginVersion.Common;

public sealed record PluginVersionDto
{
    public Guid Id { get; init; }
    public Guid PluginId { get; init; }
    public string Version { get; init; } = string.Empty;
    public string? Changelog { get; init; }
    public bool IsBreakingChange { get; init; }
    public string? MinCompatibility { get; init; }
    public string? MaxCompatibility { get; init; }
    public string? DownloadUrl { get; init; }
    public string? Checksum { get; init; }
    public long? FileSizeBytes { get; init; }
    public bool IsDeprecated { get; init; }
    public DateTime? DeprecatedAt { get; init; }
    public DateTime? DeprecationEndsAt { get; init; }
    public string? DeprecationMessage { get; init; }
    public Guid? SuccessorVersionId { get; init; }
    public bool IsLatest { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PublishedAt { get; init; }

    public static PluginVersionDto FromEntity(PluginVersionEntity entity) => new()
    {
        Id = entity.Id,
        PluginId = entity.PluginId,
        Version = entity.Version,
        Changelog = entity.Changelog,
        IsBreakingChange = entity.IsBreakingChange,
        MinCompatibility = entity.MinCompatibility,
        MaxCompatibility = entity.MaxCompatibility,
        DownloadUrl = entity.DownloadUrl,
        Checksum = entity.Checksum,
        FileSizeBytes = entity.FileSizeBytes,
        IsDeprecated = entity.IsDeprecated,
        DeprecatedAt = entity.DeprecatedAt,
        DeprecationEndsAt = entity.DeprecationEndsAt,
        DeprecationMessage = entity.DeprecationMessage,
        SuccessorVersionId = entity.SuccessorVersionId,
        IsLatest = entity.IsLatest,
        IsActive = entity.IsActive,
        CreatedAt = entity.CreatedAt,
        PublishedAt = entity.PublishedAt
    };
}
