using Application.Interfaces;
using Application.UseCases.PersonalityTemplate.Common;
using Core.Domain.Entities;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.CreateTemplate;

public class CreateTemplateHandler(IPersonalityTemplateRepository repository)
    : IRequestHandler<CreateTemplateCommand, PersonalityTemplateDto>
{
    public async Task<PersonalityTemplateDto> Handle(CreateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = new PersonalityTemplateEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Prompt = request.Prompt,
            Category = request.Category,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            IsSystem = false, // User-created templates are not system templates
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var created = await repository.CreateAsync(template, cancellationToken);
        return PersonalityTemplateDto.FromEntity(created);
    }
}