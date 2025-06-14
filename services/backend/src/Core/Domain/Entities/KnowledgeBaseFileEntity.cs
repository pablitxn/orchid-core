namespace Domain.Entities;

public class KnowledgeBaseFileEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public string MimeType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
}