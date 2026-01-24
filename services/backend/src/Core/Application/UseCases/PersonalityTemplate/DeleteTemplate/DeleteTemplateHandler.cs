using Application.Interfaces;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.DeleteTemplate;

public class DeleteTemplateHandler(IPersonalityTemplateRepository repository)
    : IRequestHandler<DeleteTemplateCommand, Unit>
{
    public async Task<Unit> Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (template == null)
            throw new EntityNotFoundException("PersonalityTemplate", request.Id);

        await repository.DeleteAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}