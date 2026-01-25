using System.ComponentModel.DataAnnotations.Schema;
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
    /// Stored as string for DB compatibility.
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets the data as JsonDocument. Not mapped to DB.
    /// </summary>
    [NotMapped]
    public JsonDocument? DataDocument
    {
        get => string.IsNullOrEmpty(Data) ? null : JsonDocument.Parse(Data);
        set => Data = value?.RootElement.GetRawText();
    }
}
