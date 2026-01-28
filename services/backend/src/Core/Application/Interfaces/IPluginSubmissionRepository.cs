using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public interface IPluginSubmissionRepository
{
    Task<PluginSubmissionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<PluginSubmissionEntity>> ListByPublisherAsync(Guid publisherId, CancellationToken cancellationToken = default);
    Task<List<PluginSubmissionEntity>> ListByStatusAsync(SubmissionStatus status, CancellationToken cancellationToken = default);
    Task<List<PluginSubmissionEntity>> ListPendingAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task CreateAsync(PluginSubmissionEntity submission, CancellationToken cancellationToken = default);
    Task UpdateAsync(PluginSubmissionEntity submission, CancellationToken cancellationToken = default);
}
