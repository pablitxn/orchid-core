using System;
using System.Collections.Generic;

namespace Core.Application.DTOs.KnowledgeBase;

public record KnowledgeBaseQueryDto
{
    public string? SearchTerm { get; init; }
    public List<string>? Tags { get; init; }
    public List<string>? MimeTypes { get; init; }
    public DateTime? CreatedAfter { get; init; }
    public DateTime? CreatedBefore { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; } = "CreatedAt";
    public bool SortDescending { get; init; } = true;
}

public record KnowledgeBaseItemDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<string> Tags { get; init; } = new();
    public string MimeType { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record KnowledgeBaseListResponseDto
{
    public List<KnowledgeBaseItemDto> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record UpdateKnowledgeBaseDto
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public List<string>? Tags { get; init; }
}