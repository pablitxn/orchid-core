namespace Domain.Entities;

/// <summary>
/// Canvas text artifact data (rich text blocks).
/// </summary>
public class CanvasTextArtifactEntity
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
    /// Text content (may include markdown or rich text).
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
