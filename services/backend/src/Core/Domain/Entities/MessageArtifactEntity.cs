namespace Domain.Entities;

/// <summary>
/// Base entity for message artifacts. Each artifact type has its own
/// specific table that references this entity via ArtifactId.
/// </summary>
public class MessageArtifactEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the message that contains this artifact.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Navigation property to the parent message.
    /// </summary>
    public MessageV2Entity? Message { get; set; }

    /// <summary>
    /// Artifact type discriminator: 'chart', 'table', 'mermaid', 'canvas_text',
    /// 'image', 'video', 'threejs', 'calendar'.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Optional display title for the artifact.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Display order within the message (0-based).
    /// </summary>
    public int Order { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties to specific artifact types (one-to-one)
    public ChartArtifactEntity? ChartArtifact { get; set; }
    public TableArtifactEntity? TableArtifact { get; set; }
    public MermaidArtifactEntity? MermaidArtifact { get; set; }
    public CanvasTextArtifactEntity? CanvasTextArtifact { get; set; }
    public ImageArtifactEntity? ImageArtifact { get; set; }
    public VideoArtifactEntity? VideoArtifact { get; set; }
    public ThreeJsArtifactEntity? ThreeJsArtifact { get; set; }
    public CalendarArtifactEntity? CalendarArtifact { get; set; }
}
