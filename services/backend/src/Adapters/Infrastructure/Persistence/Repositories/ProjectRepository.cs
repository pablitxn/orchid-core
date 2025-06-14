using Application.Interfaces;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

public class ProjectRepository(ApplicationDbContext dbContext) : IProjectRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task SaveAsync(ProjectEntity project, CancellationToken cancellationToken)
    {
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await _dbContext.Projects.FindAsync([id], cancellationToken);
        return project!;
    }
}