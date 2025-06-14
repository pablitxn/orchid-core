using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApi.IntegrationTests.MediaCenter;

public class MediaCenterIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MediaCenterIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.UseSetting("Environment", "Testing")).CreateClient();
    }

    [Fact]
    public async Task UploadKnowledgeFile_CreatesMediaAsset()
    {
        var sessionId = Guid.NewGuid().ToString();
        var content = new MultipartFormDataContent();
        var bytes = new byte[] { 1, 2, 3 };
        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(byteContent, "file", "test.png");
        content.Add(new StringContent(sessionId), "sessionId");
        content.Add(new StringContent("knowledge"), "type");

        var res = await _client.PostAsync("/api/files/upload", content);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var assets = await _client.GetFromJsonAsync<JsonElement[]>("/api/media-center/assets?mimeType=image/png");
        Assert.True(assets!.Length > 0);
    }
}