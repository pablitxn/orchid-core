using MediatR;

namespace Application.UseCases.ChatSession.GetChatSessionById;

public record GetChatSessionByIdQuery(string SessionId) : IRequest<GetChatSessionByIdDto?>;