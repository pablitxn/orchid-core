namespace Domain.Entities;

/// <summary>
/// Video artifact data.
/// </summary>
public class VideoArtifactEntity
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
    /// Video source URL.
    /// </summary>
    public string Src { get; set; } = string.Empty;
}
