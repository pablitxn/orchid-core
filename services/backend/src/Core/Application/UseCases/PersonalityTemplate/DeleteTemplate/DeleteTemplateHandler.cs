using Application.Interfaces;
using MediatR;

namespace Application.UseCases.PersonalityTemplate.DeleteTemplate;

public class DeleteTemplateHandler(IPersonalityTemplateRepository repository)
    : IRequestHandler<DeleteTemplateCommand, bool>
{
    public async Task<bool> Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        return await repository.DeleteAsync(request.Id, cancellationToken);
    }
}