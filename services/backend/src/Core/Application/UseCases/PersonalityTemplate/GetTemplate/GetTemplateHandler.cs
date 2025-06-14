using Application.Interfaces;
using Application.UseCases.PersonalityTemplate.Common;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.GetTemplate;

public class GetTemplateHandler(IPersonalityTemplateRepository repository) 
    : IRequestHandler<GetTemplateQuery, PersonalityTemplateDto?>
{
    public async Task<PersonalityTemplateDto?> Handle(GetTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(request.Id, cancellationToken);
        return template != null ? PersonalityTemplateDto.FromEntity(template) : null;
    }
}