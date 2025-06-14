using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Application.Interfaces;

public interface IMessageRepository
{
    Task<int> GetCountBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<List<DateTime>> GetMessageTimestampsBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetMessagesByHourAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}