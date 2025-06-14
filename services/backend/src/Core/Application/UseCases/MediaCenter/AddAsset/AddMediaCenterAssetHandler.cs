using Application.Interfaces;
using Core.Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.MediaCenter.AddAsset;

public class AddMediaCenterAssetHandler(IMediaCenterAssetRepository repository)
    : IRequestHandler<AddMediaCenterAssetCommand, MediaCenterAssetEntity>
{
    private readonly IMediaCenterAssetRepository _repository = repository;

    public async Task<MediaCenterAssetEntity> Handle(AddMediaCenterAssetCommand request,
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
        return asset;
    }
}