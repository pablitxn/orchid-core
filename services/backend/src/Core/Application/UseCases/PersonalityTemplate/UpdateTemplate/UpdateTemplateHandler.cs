using Application.Interfaces;
using Application.UseCases.PersonalityTemplate.Common;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.UpdateTemplate;

public class UpdateTemplateHandler(IPersonalityTemplateRepository repository)
    : IRequestHandler<UpdateTemplateCommand, PersonalityTemplateDto?>
{
    public async Task<PersonalityTemplateDto?> Handle(UpdateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(request.Id, cancellationToken);
            
        if (template == null)
            return null;
            
        // Don't allow modification of system templates' critical properties
        if (!template.IsSystem)
        {
            template.Name = request.Name;
            template.Description = request.Description;
            template.Prompt = request.Prompt;
            template.Category = request.Category;
        }
        
        // These can be updated even for system templates
        template.DisplayOrder = request.DisplayOrder;
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        
        var updated = await repository.UpdateAsync(template, cancellationToken);
        return updated != null ? PersonalityTemplateDto.FromEntity(updated) : null;
    }
}