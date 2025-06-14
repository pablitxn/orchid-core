namespace Domain.Entities;

public class AudioFileEntity
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    public Guid ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }
}