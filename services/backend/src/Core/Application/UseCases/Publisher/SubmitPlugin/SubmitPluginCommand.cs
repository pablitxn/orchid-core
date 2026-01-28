using Application.UseCases.Publisher.Common;
using MediatR;

namespace Application.UseCases.Publisher.SubmitPlugin;

public record SubmitPluginCommand(
    Guid UserId,
    string Name,
    string Slug,
    string ShortDescription,
    string? FullDescription,
    string Category,
    string? Tags,
    string Version,
    string? ManifestUrl,
    string? RepositoryUrl,
    string? Compatibility
) : IRequest<SubmissionDto>;

public sealed record SubmissionDto
{
    public Guid Id { get; init; }
    public Guid PublisherId { get; init; }
    public Guid? PluginId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string ShortDescription { get; init; } = string.Empty;
    public string? FullDescription { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? Tags { get; init; }
    public string Version { get; init; } = string.Empty;
    public string? ManifestUrl { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? Compatibility { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ReviewNotes { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
