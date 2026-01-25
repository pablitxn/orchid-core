using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Domain.Entities;

/// <summary>
/// Chart artifact data (bar, line, pie, area charts).
/// </summary>
public class ChartArtifactEntity
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
    /// Type of chart: 'bar', 'line', 'pie', 'area'.
    /// </summary>
    public string ChartType { get; set; } = string.Empty;

    /// <summary>
    /// Chart data in JSON format (labels, datasets, options).
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
