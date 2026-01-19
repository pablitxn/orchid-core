using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Tools;

/// <summary>
/// Web search tool for Agent Framework.
/// Provides web search, content fetching, and text processing capabilities.
/// </summary>
public sealed class WebSearchTool
{
    private readonly IActivityPublisher _activity;
    private readonly IChatCompletionPort _chat;
    private readonly IEmbeddingGeneratorPort _embedder;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WebSearchTool> _logger;
    private readonly ISearchProvider _search;
    private readonly IVectorStorePort _vectorStore;

    public WebSearchTool(
        ILogger<WebSearchTool> logger,
        ISearchProvider searchProvider,
        IHttpClientFactory httpClientFactory,
        IChatCompletionPort chat,
        IEmbeddingGeneratorPort embedder,
        IVectorStorePort vectorStore,
        IActivityPublisher activityPublisher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _search = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));
        _httpFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _activity = activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));
    }

    /// <summary>
    /// Search the web for information.
    /// </summary>
    [Description("Search the web for information using a search query. Returns relevant web results with titles, snippets, and URLs.")]
    public async Task<string> SearchWebAsync(
        [Description("The search query")] string query,
        [Description("Number of results to return")] int topK = 8,
        [Description("Optional site filter (e.g., 'example.com')")] string? siteFilter = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("SearchWeb → {Query}", query);
        var results = await _search.SearchAsync(query, topK, siteFilter, ct);
        var json = JsonSerializer.Serialize(results);
        await _activity.PublishAsync("tool_invocation",
            new { tool = "search_web", parameters = new { query, topK, siteFilter }, result = json }, ct);
        return json;
    }

    /// <summary>
    /// Fetch content from a URL.
    /// </summary>
    [Description("Fetch the raw HTML content from a URL. Use this to retrieve web page content for further processing.")]
    public async Task<string> FetchContentAsync(
        [Description("The URL to fetch content from")] string url,
        CancellationToken ct = default)
    {
        _logger.LogInformation("FetchContent → {Url}", url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogError("Invalid URL: {Url}", url);
            return string.Empty;
        }

        if (uri.HostNameType != UriHostNameType.Dns)
        {
            _logger.LogError("Blocked non-DNS URL: {Url}", url);
            return string.Empty;
        }

        var client = _httpFactory.CreateClient();
        try
        {
            var content = await client.GetStringAsync(uri, ct);
            await _activity.PublishAsync("tool_invocation",
                new { tool = "fetch_content", parameters = new { url }, result = content }, ct);
            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for URL {Url}", url);
        }
        catch (TaskCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Fetch content canceled for URL {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching content from URL {Url}", url);
        }

        return string.Empty;
    }

    /// <summary>
    /// Clean HTML content by removing tags and normalizing whitespace.
    /// </summary>
    [Description("Clean HTML content by removing tags, decoding entities, and normalizing whitespace. Use this to extract plain text from HTML.")]
    public string CleanContent(
        [Description("The HTML content to clean")] string html)
    {
        _logger.LogInformation("CleanContent (len={Length})", html.Length);
        var text = Regex.Replace(html, "<.*?>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "\\s+", " ").Trim();

        try
        {
            _activity.PublishAsync("tool_invocation",
                new { tool = "clean_content", parameters = new { length = html.Length }, result = text })
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish tool invocation for clean_content");
        }

        return text;
    }

    /// <summary>
    /// Summarize text content using AI.
    /// </summary>
    [Description("Summarize text content into a concise 3-sentence summary using AI. Use this to get the key points from long text.")]
    public async Task<string> SummarizeContentAsync(
        [Description("The text content to summarize")] string content,
        CancellationToken ct = default)
    {
        _logger.LogInformation("SummarizeContent (len={Length})", content.Length);
        var messages = new[]
        {
            new ChatMessage("system", "Summarize the following text in 3 sentences:"),
            new ChatMessage("user", content)
        };
        var summary = await _chat.CompleteAsync(messages, ct);
        await _activity.PublishAsync("tool_invocation",
            new { tool = "summarize_content", parameters = new { length = content.Length }, result = summary }, ct);
        return summary;
    }

    /// <summary>
    /// Store text content in the vector database for semantic search.
    /// </summary>
    [Description("Store text content from a URL in the vector database for later semantic search. The content is chunked and embedded automatically.")]
    public async Task<string> UpsertToVectorAsync(
        [Description("The source URL of the content")] string url,
        [Description("The text content to store")] string text,
        CancellationToken ct = default)
    {
        _logger.LogInformation("UpsertToVector → {Url}", url);

        var urlBytes = Encoding.UTF8.GetBytes(url);
        using var md5 = MD5.Create();
        var fileId = new Guid(md5.ComputeHash(urlBytes));

        const int MaxChunkSize = 1000;
        var chunkIds = new List<Guid>();

        if (!string.IsNullOrEmpty(text))
        {
            for (var offset = 0; offset < text.Length;)
            {
                var length = Math.Min(MaxChunkSize, text.Length - offset);
                var chunkText = text.Substring(offset, length);
                var embedding = await _embedder.EmbedAsync(chunkText, ct);
                var chunkId = Guid.NewGuid();
                var startOffset = offset;
                var endOffset = offset + length;
                var chunk = new VectorChunk(chunkId, fileId, url, startOffset, endOffset, chunkText, embedding);
                await _vectorStore.UpsertChunkAsync(chunk, ct);
                chunkIds.Add(chunkId);
                offset += length;
            }
        }
        else
        {
            _logger.LogWarning("Empty text for URL {Url}, skipping vector upsert.", url);
        }

        var result = JsonSerializer.Serialize(new { chunkIds, url });
        await _activity.PublishAsync("tool_invocation",
            new { tool = "upsert_to_vector", parameters = new { url, chunkCount = chunkIds.Count }, result }, ct);
        return result;
    }

    /// <summary>
    /// Creates AIFunction instances from this tool's methods.
    /// </summary>
    public IReadOnlyList<AIFunction> GetAIFunctions()
    {
        return new List<AIFunction>
        {
            AIFunctionFactory.Create(SearchWebAsync, this),
            AIFunctionFactory.Create(FetchContentAsync, this),
            AIFunctionFactory.Create(CleanContent, this),
            AIFunctionFactory.Create(SummarizeContentAsync, this),
            AIFunctionFactory.Create(UpsertToVectorAsync, this)
        };
    }
}
