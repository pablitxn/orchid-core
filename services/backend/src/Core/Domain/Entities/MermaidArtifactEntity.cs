namespace Domain.Entities;

/// <summary>
/// Mermaid diagram artifact data.
/// </summary>
public class MermaidArtifactEntity
{
    /// <summary>
    /// Primary key and foreign key to MessageArtifactEntity.
    /// </summary>
    public Guid ArtifactId { get; set; }

    /// <summary>
    /// Navigation property to the base artifact.
    /// </summary>
    public MessageArtifactEntity? Artifact { get; set; }

    /// <summary>
    /// Mermaid diagram definition (markdown-like syntax).
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
