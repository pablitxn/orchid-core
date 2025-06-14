using Application.Interfaces;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

public class AudioFileRepository(ApplicationDbContext dbContext) : IAudioFileRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task SaveAsync(AudioFileEntity project, CancellationToken cancellationToken)
    {
        _dbContext.AudioFiles.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}