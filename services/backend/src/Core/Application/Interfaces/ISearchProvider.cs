namespace Application.Interfaces;

public sealed record WebSearchResult(string Title, string Url, string Snippet);

public interface ISearchProvider
{
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(
        string query,
        int topK = 8,
        string? siteFilter = null,
        CancellationToken ct = default);
}