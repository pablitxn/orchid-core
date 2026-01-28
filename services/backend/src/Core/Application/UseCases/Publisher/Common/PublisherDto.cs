using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.Publisher.Common;

public sealed record PublisherDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? LogoUrl { get; init; }
    public string? SupportEmail { get; init; }
    public bool IsVerified { get; init; }
    public DateTime? VerifiedAt { get; init; }
    public PublisherStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int PluginCount { get; init; }

    public static PublisherDto FromEntity(PublisherEntity entity, int pluginCount = 0) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        Name = entity.Name,
        Bio = entity.Bio,
        WebsiteUrl = entity.WebsiteUrl,
        LogoUrl = entity.LogoUrl,
        SupportEmail = entity.SupportEmail,
        IsVerified = entity.IsVerified,
        VerifiedAt = entity.VerifiedAt,
        Status = entity.Status,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        PluginCount = pluginCount
    };
}
