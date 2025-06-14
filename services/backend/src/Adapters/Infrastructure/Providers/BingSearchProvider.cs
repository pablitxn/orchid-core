using System.Text.Json;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

public class BingSearchProvider : ISearchProvider
{
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<BingSearchProvider> _logger;

    public BingSearchProvider(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<BingSearchProvider> logger)
    {
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (config == null) throw new ArgumentNullException(nameof(config));

        var endpoint = config["Search:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = Environment.GetEnvironmentVariable("BING_SEARCH_ENDPOINT") ??
                       "https://api.bing.microsoft.com/v7.0/search";
        _endpoint = endpoint;

        var apiKey = config["Search:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("BING_SEARCH_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // throw new InvalidOperationException("Bing Search API key not configured. Set 'Search:ApiKey' in configuration or 'BING_SEARCH_API_KEY' environment variable.");
        }

        _apiKey = apiKey;
    }

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(
        string query,
        int topK = 8,
        string? siteFilter = null,
        CancellationToken ct = default)
    {
        var queryText = siteFilter != null ? $"site:{siteFilter} {query}" : query;
        var requestUri = $"{_endpoint}?q={Uri.EscapeDataString(queryText)}&count={topK}";
        try
        {
            using var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
            var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var results = new List<WebSearchResult>();
            if (root.TryGetProperty("webPages", out var webPages) &&
                webPages.TryGetProperty("value", out var value) &&
                value.ValueKind == JsonValueKind.Array)
                foreach (var item in value.EnumerateArray())
                {
                    var title = item.GetProperty("name").GetString() ?? string.Empty;
                    var url = item.GetProperty("url").GetString() ?? string.Empty;
                    var snippet = item.GetProperty("snippet").GetString() ?? string.Empty;
                    results.Add(new WebSearchResult(title, url, snippet));
                }
            else
                _logger.LogWarning("Bing search returned no webPages for query '{Query}'", query);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Bing search for query '{Query}'", query);
            return Array.Empty<WebSearchResult>();
        }
    }
}