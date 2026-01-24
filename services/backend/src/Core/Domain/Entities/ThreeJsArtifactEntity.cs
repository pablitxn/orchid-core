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
    /// </summary>
    public JsonDocument? SceneData { get; set; }
}
