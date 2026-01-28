using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.Publisher.Common;

public sealed record PublisherPluginDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Version { get; init; }
    public bool IsActive { get; init; }
    public bool IsPublic { get; init; }
    public double Rating { get; init; }
    public int ReviewCount { get; init; }
    public int InstallCount { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public SubmissionStatus? LatestSubmissionStatus { get; init; }

    public static PublisherPluginDto FromEntity(PluginEntity entity, SubmissionStatus? submissionStatus = null) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        Category = entity.Category,
        Version = entity.Version,
        IsActive = entity.IsActive,
        IsPublic = entity.IsPublic,
        Rating = entity.Rating,
        ReviewCount = entity.ReviewCount,
        InstallCount = entity.InstallCount,
        PublishedAt = entity.PublishedAt,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        LatestSubmissionStatus = submissionStatus
    };
}
