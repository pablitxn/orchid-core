using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class KnowledgeBaseFileRepository(ApplicationDbContext dbContext) : IKnowledgeBaseFileRepository
{
    private readonly ApplicationDbContext _db = dbContext;

    public async Task AddAsync(KnowledgeBaseFileEntity file, CancellationToken ct = default)
    {
        _db.KnowledgeBaseFiles.Add(file);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<KnowledgeBaseFileEntity>> SearchAsync(string? mimeType, string[]? tags,
        CancellationToken ct = default)
    {
        IQueryable<KnowledgeBaseFileEntity> query = _db.KnowledgeBaseFiles;
        if (!string.IsNullOrWhiteSpace(mimeType)) query = query.Where(f => f.MimeType == mimeType);
        if (tags is { Length: > 0 }) query = query.Where(f => f.Tags.Any(t => tags.Contains(t)));
        return await query.OrderByDescending(f => f.CreatedAt).ToListAsync(ct);
    }

    public async Task<KnowledgeBaseFileEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.KnowledgeBaseFiles.FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<(IReadOnlyList<KnowledgeBaseFileEntity> items, int totalCount)> GetPaginatedAsync(
        Guid userId,
        string? searchTerm = null,
        List<string>? tags = null,
        List<string>? mimeTypes = null,
        DateTime? createdAfter = null,
        DateTime? createdBefore = null,
        int page = 1,
        int pageSize = 20,
        string? sortBy = "CreatedAt",
        bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.KnowledgeBaseFiles.Where(f => f.UserId == userId);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(f => 
                f.Title.Contains(searchTerm) || 
                (f.Description != null && f.Description.Contains(searchTerm)));
        }

        if (tags?.Any() == true)
        {
            query = query.Where(f => f.Tags.Any(t => tags.Contains(t)));
        }

        if (mimeTypes?.Any() == true)
        {
            query = query.Where(f => mimeTypes.Contains(f.MimeType));
        }

        if (createdAfter.HasValue)
        {
            query = query.Where(f => f.CreatedAt >= createdAfter.Value);
        }

        if (createdBefore.HasValue)
        {
            query = query.Where(f => f.CreatedAt <= createdBefore.Value);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "title" => sortDescending ? query.OrderByDescending(f => f.Title) : query.OrderBy(f => f.Title),
            "updatedat" => sortDescending ? query.OrderByDescending(f => f.UpdatedAt) : query.OrderBy(f => f.UpdatedAt),
            _ => sortDescending ? query.OrderByDescending(f => f.CreatedAt) : query.OrderBy(f => f.CreatedAt)
        };

        // Apply pagination
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task UpdateAsync(KnowledgeBaseFileEntity file, CancellationToken ct = default)
    {
        _db.KnowledgeBaseFiles.Update(file);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var file = await GetByIdAsync(id, ct);
        if (file != null)
        {
            _db.KnowledgeBaseFiles.Remove(file);
            await _db.SaveChangesAsync(ct);
        }
    }
}