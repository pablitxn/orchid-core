using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Providers;

/// <summary>
///     Generates embeddings using OpenAI embedding endpoint.
/// </summary>
public class OpenAIEmbeddingGenerator : IEmbeddingGeneratorPort
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OpenAIEmbeddingGenerator(IConfiguration configuration)
    {
        _model = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
        var apiKey = configuration["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<float[]> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        var request = new { model = _model, input };
        var response = await _httpClient.PostAsJsonAsync("embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken);
        if (body?.Data == null || body.Data.Count == 0)
            throw new InvalidOperationException("OpenAI embedding response is empty");
        return body.Data[0].Embedding.Select(d => (float)d).ToArray();
    }

    private class EmbeddingResponse
    {
        public string Object { get; set; } = null!;
        public List<EmbeddingData> Data { get; set; } = new();

        public class EmbeddingData
        {
            public List<double> Embedding { get; } = new();
        }
    }
}