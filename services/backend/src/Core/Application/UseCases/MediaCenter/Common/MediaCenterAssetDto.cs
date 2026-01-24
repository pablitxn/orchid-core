using Domain.Entities;

namespace Application.UseCases.MediaCenter.Common;

public sealed record MediaCenterAssetDto
{
    public Guid Id { get; init; }
    public Guid KnowledgeBaseFileId { get; init; }
    public string MimeType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public TimeSpan? Duration { get; init; }
    public string FileUrl { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    public static MediaCenterAssetDto FromEntity(MediaCenterAssetEntity entity) => new()
    {
        Id = entity.Id,
        KnowledgeBaseFileId = entity.KnowledgeBaseFileId,
        MimeType = entity.MimeType,
        Title = entity.Title,
        Duration = entity.Duration,
        FileUrl = entity.FileUrl,
        CreatedAt = entity.CreatedAt
    };
}
