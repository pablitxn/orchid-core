using Application.Interfaces;
using MediatR;

namespace Application.UseCases.ChatSession.ArchiveChatSession;

public class ArchiveChatSessionHandler(IChatSessionRepository repo) : IRequestHandler<ArchiveChatSessionCommand>
{
    private readonly IChatSessionRepository _repo = repo;

    public async Task Handle(ArchiveChatSessionCommand request, CancellationToken cancellationToken)
    {
        await _repo.ArchiveAsync(request.Id, request.Archived, cancellationToken);
        // return Unit.Value;
    }
}