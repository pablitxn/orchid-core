using MediatR;

namespace Application.UseCases.Matrix.SendMessage;

/// <summary>
/// Command for sending a message from a Matrix user to an Orchid agent.
/// The session is identified by the combination of roomId + agentId.
/// </summary>
public sealed record SendMatrixMessageCommand(
    string MatrixUserId,
    Guid AgentId,
    string RoomId,
    string Content,
    string? ReplyToEventId = null,
    string? TriggerType = null
) : IRequest<SendMatrixMessageDto>;
