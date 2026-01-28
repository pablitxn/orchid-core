namespace Application.Interfaces;

/// <summary>
/// Manages conversation threads for agents using Microsoft Agent Framework.
/// Handles thread lifecycle, including migration from Redis-based history.
/// </summary>
public interface IAgentThreadManager
{
    /// <summary>
    /// Gets an existing thread or creates a new one for the given session.
    /// If Redis contains history for this session, it will be migrated to the thread.
    /// </summary>
    /// <param name="sessionId">The chat session identifier.</param>
    /// <param name="agentId">The agent ID to create the thread for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Thread information including whether migration occurred.</returns>
    Task<AgentThreadInfo> GetOrCreateThreadAsync(
        string sessionId,
        Guid agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrates chat history from Redis to an AgentThread if Redis history exists.
    /// This operation is idempotent - multiple calls will not duplicate messages.
    /// </summary>
    /// <param name="sessionId">The chat session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if migration occurred, false if no Redis history existed.</returns>
    Task<bool> MigrateFromRedisIfNeededAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a message to the thread for the given session.
    /// </summary>
    /// <param name="sessionId">The chat session identifier.</param>
    /// <param name="role">Message role (user, assistant, system).</param>
    /// <param name="content">Message content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddMessageAsync(
        string sessionId,
        string role,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the conversation history for a session.
    /// </summary>
    /// <param name="sessionId">The chat session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of chat messages in the session.</returns>
    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up thread resources when a session is archived or deleted.
    /// Optionally persists final state to Redis as backup.
    /// </summary>
    /// <param name="sessionId">The chat session identifier.</param>
    /// <param name="persistToRedis">Whether to save final state to Redis before cleanup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CleanupThreadAsync(
        string sessionId,
        bool persistToRedis = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a session has an active thread.
    /// </summary>
    /// <param name="sessionId">The chat session identifier.</param>
    /// <returns>True if an active thread exists for the session.</returns>
    bool HasActiveThread(string sessionId);

    /// <summary>
    /// Gets session IDs of threads that have been idle longer than the specified timeout.
    /// </summary>
    /// <param name="idleTimeout">The idle timeout duration.</param>
    /// <returns>Collection of expired session IDs.</returns>
    IReadOnlyList<string> GetExpiredSessionIds(TimeSpan idleTimeout);

    /// <summary>
    /// Gets the current count of active threads.
    /// </summary>
    /// <returns>Number of active threads in memory.</returns>
    int GetActiveThreadCount();
}

/// <summary>
/// Information about an agent thread.
/// </summary>
/// <param name="SessionId">The session identifier.</param>
/// <param name="MessageCount">Number of messages in the thread.</param>
/// <param name="WasMigrated">Whether history was migrated from Redis.</param>
/// <param name="CreatedAt">When the thread was created.</param>
public sealed record AgentThreadInfo(
    string SessionId,
    int MessageCount,
    bool WasMigrated,
    DateTime CreatedAt);
