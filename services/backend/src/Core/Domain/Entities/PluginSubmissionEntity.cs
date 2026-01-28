using Domain.Enums;

namespace Domain.Entities;

public class PluginSubmissionEntity
{
    public Guid Id { get; set; }
    public Guid PublisherId { get; set; }
    public Guid? PluginId { get; set; }

    // Submission data
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string? FullDescription { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string? ManifestUrl { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? Compatibility { get; set; }

    // Review fields
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public Guid? ReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public PublisherEntity Publisher { get; set; } = null!;
    public PluginEntity? Plugin { get; set; }
}
