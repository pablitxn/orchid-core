namespace Domain.Entities;

/// <summary>
/// Image artifact data.
/// </summary>
public class ImageArtifactEntity
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
    /// Image source URL or base64 data URI.
    /// </summary>
    public string Src { get; set; } = string.Empty;

    /// <summary>
    /// Alt text for accessibility.
    /// </summary>
    public string? Alt { get; set; }
}
