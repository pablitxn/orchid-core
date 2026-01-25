using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Domain.Entities;

/// <summary>
/// Three.js 3D scene artifact data.
/// </summary>
public class ThreeJsArtifactEntity
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
    /// Scene data in JSON format (objects, lights, camera, materials).
    /// Stored as string for DB compatibility.
    /// </summary>
    public string? SceneData { get; set; }

    /// <summary>
    /// Gets or sets the scene data as JsonDocument. Not mapped to DB.
    /// </summary>
    [NotMapped]
    public JsonDocument? SceneDataDocument
    {
        get => string.IsNullOrEmpty(SceneData) ? null : JsonDocument.Parse(SceneData);
        set => SceneData = value?.RootElement.GetRawText();
    }
}
