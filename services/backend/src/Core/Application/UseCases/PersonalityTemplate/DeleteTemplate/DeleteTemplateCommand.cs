using MediatR;

namespace Application.UseCases.PersonalityTemplate.DeleteTemplate;

public record DeleteTemplateCommand(Guid Id) : IRequest<bool>;