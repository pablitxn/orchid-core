using Domain.Entities;

namespace Core.Domain.Entities;

public class PersonalityTemplateEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = false; // System templates cannot be deleted
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<AgentEntity> Agents { get; set; } = new List<AgentEntity>();
}