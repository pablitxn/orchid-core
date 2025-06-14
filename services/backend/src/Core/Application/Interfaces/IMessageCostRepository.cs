using Domain.Entities;

namespace Application.Interfaces;

public interface IMessageCostRepository
{
    Task<MessageCostEntity> CreateAsync(MessageCostEntity messageCost, CancellationToken cancellationToken = default);
    Task<MessageCostEntity?> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MessageCostEntity>> GetByUserIdAsync(Guid userId, int? limit = null, CancellationToken cancellationToken = default);
    Task<int> GetTotalCostByUserIdAsync(Guid userId, DateTime? since = null, CancellationToken cancellationToken = default);
}