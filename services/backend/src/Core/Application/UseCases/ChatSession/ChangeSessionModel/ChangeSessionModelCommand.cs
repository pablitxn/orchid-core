using MediatR;

namespace Application.UseCases.ChatSession.ChangeSessionModel;

/// <summary>
/// Command to change the model override for a chat session.
/// </summary>
/// <param name="SessionId">The session ID (string identifier).</param>
/// <param name="UserId">The user requesting the change (for authorization).</param>
/// <param name="ModelId">The model ID to set as override.</param>
public sealed record ChangeSessionModelCommand(
    string SessionId,
    Guid UserId,
    string ModelId) : IRequest<ChangeSessionModelDto>;
