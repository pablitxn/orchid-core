using Domain.Entities;

namespace Application.Interfaces;

public interface IProjectRepository
{
    Task SaveAsync(ProjectEntity project, CancellationToken cancellationToken);

    Task<ProjectEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}