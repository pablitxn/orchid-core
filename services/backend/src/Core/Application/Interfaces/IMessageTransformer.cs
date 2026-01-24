using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Port for transforming legacy V1 messages to V2 format during migration.
/// </summary>
public interface IMessageTransformer
{
    /// <summary>
    /// Transforms legacy ChatMessage records to V2 entities.
    /// </summary>
    /// <param name="sessionId">The session ID being migrated.</param>
    /// <param name="chatSessionId">The Guid of the ChatSessionEntity.</param>
    /// <param name="legacyMessages">The legacy messages from thread manager or cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of transformed V2 message entities.</returns>
    Task<List<MessageV2Entity>> TransformLegacyMessagesAsync(
        string sessionId,
        Guid chatSessionId,
        IReadOnlyList<ChatMessage> legacyMessages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a session has been migrated to V2.
    /// </summary>
    /// <param name="sessionId">The session ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the session has been migrated.</returns>
    Task<bool> IsSessionMigratedAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a session as migrated after successful transformation.
    /// </summary>
    /// <param name="sessionId">The session ID to mark.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkSessionMigratedAsync(string sessionId, CancellationToken cancellationToken = default);
}
