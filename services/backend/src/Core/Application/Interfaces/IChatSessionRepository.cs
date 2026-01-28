using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public interface IChatSessionRepository
{
    Task<List<ChatSessionEntity>> ListAsync(
        Guid userId,
        bool archived,
        Guid? agentId,
        Guid? teamId,
        DateTime? startDate,
        DateTime? endDate,
        InteractionType? type,
        CancellationToken cancellationToken);

    Task<List<ChatSessionEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<ChatSessionEntity?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken);
    Task CreateAsync(ChatSessionEntity session, CancellationToken cancellationToken);
    Task ArchiveAsync(Guid id, bool archived, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Gets conversation count for an agent
    /// </summary>
    Task<int> CountByAgentIdAsync(Guid agentId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the most recent session for an agent
    /// </summary>
    Task<DateTime?> GetLastUsedByAgentIdAsync(Guid agentId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets aggregated stats (conversation count and last used) for multiple agents.
    /// Returns a dictionary keyed by AgentId.
    /// </summary>
    Task<Dictionary<Guid, (int Count, DateTime? LastUsed)>> GetStatsForAgentsAsync(
        IEnumerable<Guid> agentIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the model override for a chat session.
    /// </summary>
    /// <param name="sessionId">The session ID (string identifier).</param>
    /// <param name="modelOverride">The model to override with (null to clear).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated session, or null if not found.</returns>
    Task<ChatSessionEntity?> UpdateModelOverrideAsync(
        string sessionId,
        string? modelOverride,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the model override for a session, if any.
    /// </summary>
    /// <param name="sessionId">The session ID (string identifier).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model override, or null if not set.</returns>
    Task<string?> GetModelOverrideAsync(
        string sessionId,
        CancellationToken cancellationToken);
}