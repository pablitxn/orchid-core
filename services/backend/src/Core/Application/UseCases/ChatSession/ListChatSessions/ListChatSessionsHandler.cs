using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.ChatSession.ListChatSessions;

public class ListChatSessionsHandler(IChatSessionRepository repo)
    : IRequestHandler<ListChatSessionsQuery, List<ChatSessionEntity>>
{
    private readonly IChatSessionRepository _repo = repo;

    public async Task<List<ChatSessionEntity>> Handle(ListChatSessionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _repo.ListAsync(
            request.UserId,
            request.Archived,
            request.AgentId,
            request.TeamId,
            request.StartDate,
            request.EndDate,
            request.Type,
            cancellationToken);
    }
}