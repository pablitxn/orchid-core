using Application.UseCases.MediaCenter.Common;
using Core.Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.MediaCenter.AddAsset;

public class AddMediaCenterAssetHandler(IMediaCenterAssetRepository repository)
    : IRequestHandler<AddMediaCenterAssetCommand, MediaCenterAssetDto>
{
    private readonly IMediaCenterAssetRepository _repository = repository;

    public async Task<MediaCenterAssetDto> Handle(AddMediaCenterAssetCommand request,
        CancellationToken cancellationToken)
    {
        var asset = new MediaCenterAssetEntity
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseFileId = request.KnowledgeBaseFileId,
            MimeType = request.MimeType,
            Title = request.Title,
            Duration = request.Duration,
            FileUrl = request.FileUrl,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(asset, cancellationToken);
        return MediaCenterAssetDto.FromEntity(asset);
    }
}
