using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Domain.Entities;
using Domain.Entities;

namespace Core.Application.Interfaces;

public interface IMediaCenterAssetRepository
{
    Task AddAsync(MediaCenterAssetEntity asset, CancellationToken ct = default);

    Task<IReadOnlyList<MediaCenterAssetEntity>> SearchAsync(
        string? mimeType,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);
    
    Task<List<MediaCenterAssetEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}