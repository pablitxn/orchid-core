using MediatR;

namespace Application.UseCases.ChatSession.DeleteChatSessions;

public record DeleteChatSessionsCommand(IEnumerable<Guid> Ids) : IRequest;