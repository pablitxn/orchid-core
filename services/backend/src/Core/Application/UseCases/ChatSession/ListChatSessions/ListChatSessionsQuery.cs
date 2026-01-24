using Application.UseCases.ChatSession.Common;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.ChatSession.ListChatSessions;

public record ListChatSessionsQuery(
    Guid UserId,
    bool Archived,
    Guid? AgentId,
    Guid? TeamId,
    DateTime? StartDate,
    DateTime? EndDate,
    InteractionType? Type) : IRequest<List<ChatSessionDto>>;
