using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Core.Application.Interfaces;

public interface IMessageRepository
{
    // V1 Methods (legacy placeholders)
    Task<int> GetCountBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<List<DateTime>> GetMessageTimestampsBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetMessagesByHourAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    // V2 Methods
    Task<MessageV2Entity> CreateAsync(MessageV2Entity message, CancellationToken cancellationToken = default);
    Task<MessageV2Entity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages for a session with cursor-based pagination.
    /// </summary>
    /// <param name="sessionId">The session ID to query.</param>
    /// <param name="limit">Maximum number of messages to return.</param>
    /// <param name="beforeTimestamp">Return messages before this timestamp (for backward pagination).</param>
    /// <param name="afterTimestamp">Return messages after this timestamp (for forward pagination).</param>
    /// <param name="cursorMessageId">Message ID for tie-breaking when paginating.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<MessageV2Entity>> GetBySessionIdAsync(
        string sessionId,
        int limit = 50,
        long? beforeTimestamp = null,
        long? afterTimestamp = null,
        Guid? cursorMessageId = null,
        CancellationToken cancellationToken = default);

    Task<int> GetCountBySessionIdV2Async(string sessionId, CancellationToken cancellationToken = default);
    Task UpdateAsync(MessageV2Entity message, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task BulkCreateAsync(IEnumerable<MessageV2Entity> messages, CancellationToken cancellationToken = default);
}