using Core.Domain.Entities;

namespace Application.UseCases.PersonalityTemplate.Common;

public sealed record PersonalityTemplateDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    
    public static PersonalityTemplateDto FromEntity(PersonalityTemplateEntity entity)
    {
        return new PersonalityTemplateDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Prompt = entity.Prompt,
            Category = entity.Category,
            IsSystem = entity.IsSystem,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}