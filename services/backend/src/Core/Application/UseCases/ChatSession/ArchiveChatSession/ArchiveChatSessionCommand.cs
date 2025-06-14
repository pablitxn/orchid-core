using MediatR;

namespace Application.UseCases.ChatSession.ArchiveChatSession;

public record ArchiveChatSessionCommand(Guid Id, bool Archived) : IRequest;