using Application.Interfaces;
using Application.UseCases.PersonalityTemplate.Common;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.ListTemplates;

public class ListTemplatesHandler(IPersonalityTemplateRepository repository) 
    : IRequestHandler<ListTemplatesQuery, List<PersonalityTemplateDto>>
{
    public async Task<List<PersonalityTemplateDto>> Handle(ListTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await repository.ListAsync(request.IncludeInactive, cancellationToken);
        return templates.Select(PersonalityTemplateDto.FromEntity).ToList();
    }
}