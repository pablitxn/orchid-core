using System.Text.Json;

namespace Domain.Entities;

/// <summary>
/// Table artifact data (rows and columns).
/// </summary>
public class TableArtifactEntity
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
    /// Table data in JSON format (headers, rows).
    /// </summary>
    public JsonDocument? Data { get; set; }
}
