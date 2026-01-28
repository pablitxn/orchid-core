using Domain.Entities;

namespace Application.Interfaces;

public interface IPublisherRepository
{
    Task<PublisherEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PublisherEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<PublisherEntity>> ListAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(PublisherEntity publisher, CancellationToken cancellationToken = default);
    Task UpdateAsync(PublisherEntity publisher, CancellationToken cancellationToken = default);
}
