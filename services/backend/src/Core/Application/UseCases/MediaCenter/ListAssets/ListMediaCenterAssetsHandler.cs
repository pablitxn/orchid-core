using Application.UseCases.MediaCenter.Common;
using Core.Application.Interfaces;
using MediatR;

namespace Application.UseCases.MediaCenter.ListAssets;

public class ListMediaCenterAssetsHandler(IMediaCenterAssetRepository repository)
    : IRequestHandler<ListMediaCenterAssetsQuery, List<MediaCenterAssetDto>>
{
    private readonly IMediaCenterAssetRepository _repository = repository;

    public async Task<List<MediaCenterAssetDto>> Handle(ListMediaCenterAssetsQuery request,
        CancellationToken cancellationToken)
    {
        var assets = await _repository.SearchAsync(request.MimeType, null, null, cancellationToken);
        return assets.Select(MediaCenterAssetDto.FromEntity).ToList();
    }
}
