using Domain.Entities;

namespace Application.UseCases.Project.Common;

public sealed record AudioFileDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string Url { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    public static AudioFileDto FromEntity(AudioFileEntity entity) => new()
    {
        Id = entity.Id,
        FileName = entity.FileName,
        ContentType = entity.ContentType,
        FileSize = entity.FileSize,
        Url = entity.Url,
        CreatedAt = entity.CreatedAt
    };
}

public sealed record ProjectDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TargetLanguage { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public AudioFileDto? AudioFile { get; init; }

    public static ProjectDto FromEntity(ProjectEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        TargetLanguage = entity.TargetLanguage,
        CreatedAt = entity.CreatedAt,
        AudioFile = entity.AudioFile != null ? AudioFileDto.FromEntity(entity.AudioFile) : null
    };
}
