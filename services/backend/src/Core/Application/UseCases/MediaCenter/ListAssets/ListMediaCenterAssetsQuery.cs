using Application.UseCases.MediaCenter.Common;
using MediatR;

namespace Application.UseCases.MediaCenter.ListAssets;

public record ListMediaCenterAssetsQuery(string? MimeType) : IRequest<List<MediaCenterAssetDto>>;
