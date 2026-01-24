using Application.Interfaces;
using Application.UseCases.ChatSession.Common;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.ChatSession.CreateChatSession;

public class CreateChatSessionHandler(IChatSessionRepository repo)
    : IRequestHandler<CreateChatSessionCommand, ChatSessionDto>
{
    private readonly IChatSessionRepository _repo = repo;

    public async Task<ChatSessionDto> Handle(CreateChatSessionCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var entity = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            AgentId = request.AgentId,
            SessionId = request.SessionId,
            Title = request.Title,
            IsArchived = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _repo.CreateAsync(entity, cancellationToken);
        return ChatSessionDto.FromEntity(entity);
    }
}
