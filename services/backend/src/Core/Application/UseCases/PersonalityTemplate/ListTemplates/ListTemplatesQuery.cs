using Application.UseCases.PersonalityTemplate.Common;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.ListTemplates;

public record ListTemplatesQuery(bool IncludeInactive = false) : IRequest<List<PersonalityTemplateDto>>;