using Application.UseCases.PersonalityTemplate.Common;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.CreateTemplate;

public record CreateTemplateCommand(
    string Name,
    string Description,
    string Prompt,
    string Category,
    int DisplayOrder = 999,
    bool IsActive = true
) : IRequest<PersonalityTemplateDto>;