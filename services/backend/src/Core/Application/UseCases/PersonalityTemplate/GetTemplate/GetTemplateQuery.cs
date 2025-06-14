using Application.UseCases.PersonalityTemplate.Common;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.GetTemplate;

public record GetTemplateQuery(Guid Id) : IRequest<PersonalityTemplateDto?>;