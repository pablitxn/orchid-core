namespace Domain.Entities;

public class MediaCenterAssetEntity
{
    public Guid Id { get; set; }
    public Guid KnowledgeBaseFileId { get; set; }
    public KnowledgeBaseFileEntity? KnowledgeBaseFile { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public TimeSpan? Duration { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}