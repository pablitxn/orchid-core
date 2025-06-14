using Application.Interfaces;
using Core.Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class MediaCenterAssetRepository(ApplicationDbContext db) : IMediaCenterAssetRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task AddAsync(MediaCenterAssetEntity asset, CancellationToken ct = default)
    {
        _db.MediaCenterAssets.Add(asset);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MediaCenterAssetEntity>> SearchAsync(string? mimeType, DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        IQueryable<MediaCenterAssetEntity> query = _db.MediaCenterAssets;
        if (!string.IsNullOrWhiteSpace(mimeType))
            query = query.Where(a => a.MimeType == mimeType);
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);
        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
    }

    public async Task<List<MediaCenterAssetEntity>> GetByUserIdAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.MediaCenterAssets
            .Include(a => a.KnowledgeBaseFile)
            .Where(a => a.KnowledgeBaseFile!.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}