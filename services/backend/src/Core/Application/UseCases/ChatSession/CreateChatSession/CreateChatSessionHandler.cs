using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.ChatSession.CreateChatSession;

public class CreateChatSessionHandler(IChatSessionRepository repo)
    : IRequestHandler<CreateChatSessionCommand, ChatSessionEntity>
{
    private readonly IChatSessionRepository _repo = repo;

    public async Task<ChatSessionEntity> Handle(CreateChatSessionCommand request, CancellationToken cancellationToken)
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
        return entity;
    }
}