using System.Text.Json;

namespace Domain.Entities;

/// <summary>
/// Calendar artifact data (events display).
/// </summary>
public class CalendarArtifactEntity
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
    /// Calendar events in JSON format (title, start, end, color, etc.).
    /// </summary>
    public JsonDocument? Events { get; set; }
}
