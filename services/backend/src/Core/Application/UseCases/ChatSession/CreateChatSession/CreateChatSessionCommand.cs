using Domain.Entities;
using MediatR;

namespace Application.UseCases.ChatSession.CreateChatSession;

public record CreateChatSessionCommand(Guid UserId, Guid AgentId, string SessionId, string? Title) : IRequest<ChatSessionEntity>;