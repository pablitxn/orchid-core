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
}