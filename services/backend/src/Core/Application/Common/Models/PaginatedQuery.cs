namespace Application.Common.Models;

/// <summary>
/// Base class for paginated queries
/// </summary>
public abstract class PaginatedQuery
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page
    {
        get => _page;
        set => _page = value > 0 ? value : 1;
    }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > 0 && value <= 100 ? value : 20;
    }

    /// <summary>
    /// Number of items to skip
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Number of items to take
    /// </summary>
    public int Take => PageSize;
}

/// <summary>
/// Generic paginated result
/// </summary>
public class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PaginatedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public static PaginatedResult<T> Empty(int page = 1, int pageSize = 20)
        => new(Array.Empty<T>(), 0, page, pageSize);
}