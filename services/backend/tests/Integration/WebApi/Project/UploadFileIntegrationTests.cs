using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApi.IntegrationTests.Project;

public class UploadFileIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.WithWebHostBuilder(builder =>
    {
        // Override settings if necessary (e.g., use in-memory DB, custom services, etc.)
        builder.UseSetting("Environment", "Testing");
    }).CreateClient();

    [Fact]
    public async Task UploadFile_ReturnsOkAndProjectId()
    {
        // Arrange: Create a multipart form data request with an audio file and target language.
        var content = new MultipartFormDataContent();

        // Prepare a dummy audio file content (as a byte array)
        var dummyFileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake audio content"));
        dummyFileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");
        content.Add(dummyFileContent, "audioFile", "test.mp3");

        // Add target language
        content.Add(new StringContent("en"), "targetLanguage");

        // Act: Send POST request to the upload endpoint.
        var response = await _client.PostAsync("/api/project/upload", content);

        // Assert: Check if the response status is OK.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Read the JSON response and verify that a projectId is returned.
        var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(responseJson.TryGetProperty("projectId", out var projectIdProperty));
        Assert.False(string.IsNullOrEmpty(projectIdProperty.GetString()));
    }
}