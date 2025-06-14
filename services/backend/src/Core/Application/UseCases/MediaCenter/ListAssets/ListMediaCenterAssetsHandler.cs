using Application.Interfaces;
using Core.Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.MediaCenter.ListAssets;

public class ListMediaCenterAssetsHandler(IMediaCenterAssetRepository repository)
    : IRequestHandler<ListMediaCenterAssetsQuery, List<MediaCenterAssetEntity>>
{
    private readonly IMediaCenterAssetRepository _repository = repository;

    public async Task<List<MediaCenterAssetEntity>> Handle(ListMediaCenterAssetsQuery request,
        CancellationToken cancellationToken)
    {
        var assets = await _repository.SearchAsync(request.MimeType, null, null, cancellationToken);
        return assets.ToList();
    }
}