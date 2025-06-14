using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Exceptions;
using MediatR;

namespace Core.Application.UseCases.KnowledgeBase.DeleteKnowledgeBase;

public record DeleteKnowledgeBaseCommand(
    Guid Id,
    Guid UserId
) : IRequest<Unit>;

public class DeleteKnowledgeBaseHandler : IRequestHandler<DeleteKnowledgeBaseCommand, Unit>
{
    private readonly IKnowledgeBaseFileRepository _repository;

    public DeleteKnowledgeBaseHandler(IKnowledgeBaseFileRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(
        DeleteKnowledgeBaseCommand request,
        CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (item == null)
        {
            throw new EntityNotFoundException($"Knowledge base item with ID {request.Id} not found");
        }

        // Ensure user owns this document
        if (item.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You do not have access to this document");
        }

        // Delete database record
        await _repository.DeleteAsync(request.Id, cancellationToken);

        return Unit.Value;
    }
}