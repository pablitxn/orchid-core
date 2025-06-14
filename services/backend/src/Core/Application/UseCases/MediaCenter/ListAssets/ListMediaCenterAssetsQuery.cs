using Domain.Entities;
using MediatR;

namespace Application.UseCases.MediaCenter.ListAssets;

public record ListMediaCenterAssetsQuery(string? MimeType) : IRequest<List<MediaCenterAssetEntity>>;