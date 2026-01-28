namespace Domain.Entities;

/// <summary>
/// Represents a specific version of a plugin with its metadata, changelog, and deprecation status.
/// </summary>
public class PluginVersionEntity
{
    public Guid Id { get; set; }
    public Guid PluginId { get; set; }

    /// <summary>
    /// Semantic version string (e.g., "1.2.3", "2.0.0-beta.1").
    /// Validated via SemanticVersion value object before persistence.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Release notes describing changes in this version.
    /// </summary>
    public string? Changelog { get; set; }

    /// <summary>
    /// Indicates if this version introduces breaking changes (major version bump).
    /// </summary>
    public bool IsBreakingChange { get; set; }

    /// <summary>
    /// Minimum agent/platform compatibility requirement (e.g., "agent-v2").
    /// </summary>
    public string? MinCompatibility { get; set; }

    /// <summary>
    /// Maximum agent/platform compatibility (if there's an upper bound).
    /// </summary>
    public string? MaxCompatibility { get; set; }

    /// <summary>
    /// URL to download this version's package/manifest.
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Checksum for integrity verification (e.g., "sha256:abc123...").
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// File size in bytes (for display/download estimation).
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// Cached OpenAPI specification for this version (if applicable to OpenAPI plugins).
    /// </summary>
    public string? OpenApiSpecCache { get; set; }

    /// <summary>
    /// Base URL for this version's API (if different from parent plugin).
    /// </summary>
    public string? BaseUrl { get; set; }

    // Deprecation fields

    /// <summary>
    /// Indicates if this version is deprecated.
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// When the deprecation was announced.
    /// </summary>
    public DateTime? DeprecatedAt { get; set; }

    /// <summary>
    /// When the deprecation period ends (typically 60 days after DeprecatedAt).
    /// After this date, the version may be removed or blocked.
    /// </summary>
    public DateTime? DeprecationEndsAt { get; set; }

    /// <summary>
    /// User-facing message explaining the deprecation and recommended action.
    /// </summary>
    public string? DeprecationMessage { get; set; }

    /// <summary>
    /// Reference to the recommended version to upgrade to.
    /// </summary>
    public Guid? SuccessorVersionId { get; set; }

    // Status fields

    /// <summary>
    /// Indicates if this version is active and can be installed.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates if this is the latest stable version for the plugin.
    /// Only one version per plugin should have this flag set to true.
    /// </summary>
    public bool IsLatest { get; set; }

    // Audit fields

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this version was made publicly available.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// User who published this version.
    /// </summary>
    public Guid? PublishedBy { get; set; }

    // Navigation properties

    public PluginEntity Plugin { get; set; } = null!;
    public PluginVersionEntity? SuccessorVersion { get; set; }
}
