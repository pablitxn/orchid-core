using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface IKnowledgeBaseFileRepository
{
    Task AddAsync(KnowledgeBaseFileEntity file, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeBaseFileEntity>> SearchAsync(
        string? mimeType,
        string[]? tags,
        CancellationToken ct = default);

    Task<KnowledgeBaseFileEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<KnowledgeBaseFileEntity> items, int totalCount)> GetPaginatedAsync(
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
        CancellationToken cancellationToken = default);

    Task UpdateAsync(KnowledgeBaseFileEntity file, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}