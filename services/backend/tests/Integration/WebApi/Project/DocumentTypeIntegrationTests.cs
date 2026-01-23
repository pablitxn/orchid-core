using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Enums;
using Infrastructure.Persistence;
using Integration.Shared.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests.Project;

[Collection("Integration")]
public class DocumentTypeIntegrationTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public DocumentTypeIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_WithKnowledgeType_PersistsEnum()
    {
        var sessionId = Guid.NewGuid().ToString();
        var content = new MultipartFormDataContent();
        var bytes = new byte[] { 1, 2, 3 };
        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(byteContent, "file", "test.txt");
        content.Add(new StringContent(sessionId), "sessionId");
        content.Add(new StringContent("knowledge"), "type");

        var response = await _client.PostAsync("/api/files/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fileName = json.GetProperty("fileName").GetString();
        Assert.False(string.IsNullOrEmpty(fileName));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = db.Documents.Single(d => d.FileName == fileName);
        Assert.Equal(DocumentEnum.KnowledgeBase, entity.Enum);
    }

    [Fact]
    public async Task Upload_WithInvalidType_ReturnsBadRequest()
    {
        var sessionId = Guid.NewGuid().ToString();
        var content = new MultipartFormDataContent();
        var bytes = new byte[] { 1, 2, 3 };
        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(byteContent, "file", "test.txt");
        content.Add(new StringContent(sessionId), "sessionId");
        content.Add(new StringContent("foo"), "type");

        var response = await _client.PostAsync("/api/files/upload", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
