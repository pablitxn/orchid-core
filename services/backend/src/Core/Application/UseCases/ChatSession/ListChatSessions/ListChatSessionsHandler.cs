using Application.Interfaces;
using Application.UseCases.ChatSession.Common;
using MediatR;

namespace Application.UseCases.ChatSession.ListChatSessions;

public class ListChatSessionsHandler(IChatSessionRepository repo)
    : IRequestHandler<ListChatSessionsQuery, List<ChatSessionDto>>
{
    private readonly IChatSessionRepository _repo = repo;

    public async Task<List<ChatSessionDto>> Handle(ListChatSessionsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _repo.ListAsync(
            request.UserId,
            request.Archived,
            request.AgentId,
            request.TeamId,
            request.StartDate,
            request.EndDate,
            request.Type,
            cancellationToken);

        return entities.Select(ChatSessionDto.FromEntity).ToList();
    }
}
