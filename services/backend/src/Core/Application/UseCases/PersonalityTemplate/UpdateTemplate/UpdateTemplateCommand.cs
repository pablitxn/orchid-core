using Application.UseCases.PersonalityTemplate.Common;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.UpdateTemplate;

public record UpdateTemplateCommand(
    Guid Id,
    string Name,
    string Description,
    string Prompt,
    string Category,
    int DisplayOrder,
    bool IsActive
) : IRequest<PersonalityTemplateDto?>;