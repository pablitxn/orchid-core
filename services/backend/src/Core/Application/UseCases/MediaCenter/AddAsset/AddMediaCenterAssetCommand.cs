using Domain.Entities;
using MediatR;

namespace Application.UseCases.MediaCenter.AddAsset;

public record AddMediaCenterAssetCommand(
    Guid KnowledgeBaseFileId,
    string MimeType,
    string Title,
    string FileUrl,
    TimeSpan? Duration
) : IRequest<MediaCenterAssetEntity>;