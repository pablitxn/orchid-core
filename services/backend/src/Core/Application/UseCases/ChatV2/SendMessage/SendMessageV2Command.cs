using Application.UseCases.ChatV2.Common;
using MediatR;

namespace Application.UseCases.ChatV2.SendMessage;

/// <summary>
/// Command to send a new message in a chat session.
/// </summary>
/// <param name="SessionId">The session ID to send the message to.</param>
/// <param name="UserId">The authenticated user's ID.</param>
/// <param name="Content">The text content of the message.</param>
/// <param name="Artifacts">Optional list of artifacts attached to the message.</param>
public sealed record SendMessageV2Command(
    string SessionId,
    Guid UserId,
    string Content,
    List<ArtifactDto>? Artifacts = null
) : IRequest<MessageV2Dto>;
