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
    /// </summary>
    public JsonDocument? Data { get; set; }
}
