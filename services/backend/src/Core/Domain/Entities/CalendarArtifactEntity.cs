using System.ComponentModel.DataAnnotations.Schema;
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
    /// Stored as string for DB compatibility; use EventsJson for JsonDocument access.
    /// </summary>
    public string? Events { get; set; }

    /// <summary>
    /// Gets or sets the events as JsonDocument. Not mapped to DB.
    /// </summary>
    [NotMapped]
    public JsonDocument? EventsDocument
    {
        get => string.IsNullOrEmpty(Events) ? null : JsonDocument.Parse(Events);
        set => Events = value?.RootElement.GetRawText();
    }
}
