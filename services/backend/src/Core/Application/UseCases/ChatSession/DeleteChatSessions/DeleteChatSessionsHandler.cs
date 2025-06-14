using Application.Interfaces;
using MediatR;

namespace Application.UseCases.ChatSession.DeleteChatSessions;

public class DeleteChatSessionsHandler(IChatSessionRepository repo) : IRequestHandler<DeleteChatSessionsCommand>
{
    private readonly IChatSessionRepository _repo = repo;

    public async Task Handle(DeleteChatSessionsCommand request, CancellationToken cancellationToken)
    {
        await _repo.DeleteManyAsync(request.Ids, cancellationToken);
        // return Unit.Value;
    }
}