using Domain.Entities;

namespace Application.Interfaces;

public interface IAudioFileRepository
{
    Task SaveAsync(AudioFileEntity project, CancellationToken cancellationToken);
}