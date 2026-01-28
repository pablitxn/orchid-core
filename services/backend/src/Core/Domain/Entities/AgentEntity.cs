using Core.Domain.Entities;
using Domain.Enums;

namespace Domain.Entities;

public class AgentEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Personality { get; set; }
    
    // Reference to PersonalityTemplate
    public Guid? PersonalityTemplateId { get; set; }
    public virtual PersonalityTemplateEntity? PersonalityTemplate { get; set; }

    public string? Language { get; set; }

    /// <summary>
    /// Optional model override for this agent. If null, the default model is used.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Fallback model to use when the primary model is unavailable.
    /// </summary>
    public string? FallbackModel { get; set; }

    /// <summary>
    /// Model selection preference. When set, the system will choose
    /// an appropriate model from the catalog based on this preference.
    /// Takes precedence over FallbackModel but not over direct Model configuration.
    /// </summary>
    public ModelPreference? ModelPreference { get; set; }

    public Guid[] PluginIds { get; set; } = [];

    // Ownership and visibility
    public Guid? UserId { get; set; }
    public virtual UserEntity? User { get; set; }
    
    public bool IsPublic { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Soft delete fields
    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    public bool IsInRecycleBin { get; set; } = false;

    public DateTime? RecycleBinExpiresAt { get; set; }
}