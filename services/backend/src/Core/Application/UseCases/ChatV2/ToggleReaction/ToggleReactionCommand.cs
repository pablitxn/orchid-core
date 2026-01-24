using Application.UseCases.ChatV2.Common;
using MediatR;

namespace Application.UseCases.ChatV2.ToggleReaction;

/// <summary>
/// Command to toggle a reaction on a message.
/// If the reaction exists, it will be removed; otherwise, it will be added.
/// </summary>
/// <param name="SessionId">The session ID containing the message.</param>
/// <param name="MessageId">The message ID to react to.</param>
/// <param name="UserId">The authenticated user's ID.</param>
/// <param name="Emoji">The emoji reaction.</param>
/// <param name="Type">The reaction type (e.g., "like", "dislike", "emoji").</param>
public sealed record ToggleReactionCommand(
    string SessionId,
    Guid MessageId,
    Guid UserId,
    string Emoji,
    string Type
) : IRequest<ToggleReactionDto>;

/// <summary>
/// Result of the toggle reaction operation.
/// </summary>
/// <param name="MessageId">The message that was reacted to.</param>
/// <param name="Reaction">The reaction that was added, or null if removed.</param>
/// <param name="WasAdded">True if the reaction was added, false if removed.</param>
public sealed record ToggleReactionDto(
    string MessageId,
    ReactionDto? Reaction,
    bool WasAdded
);
