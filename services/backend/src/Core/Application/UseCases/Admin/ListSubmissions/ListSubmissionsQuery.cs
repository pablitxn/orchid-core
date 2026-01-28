using Domain.Enums;
using MediatR;

namespace Application.UseCases.Admin.ListSubmissions;

public record ListSubmissionsQuery(SubmissionStatus? Status = null) : IRequest<List<AdminSubmissionDto>>;

public sealed record AdminSubmissionDto
{
    public Guid Id { get; init; }
    public Guid PublisherId { get; init; }
    public string PublisherName { get; init; } = string.Empty;
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
    public SubmissionStatus Status { get; init; }
    public Guid? ReviewedBy { get; init; }
    public string? ReviewNotes { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
