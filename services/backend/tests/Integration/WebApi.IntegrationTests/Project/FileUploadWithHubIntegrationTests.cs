using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using WebApi.Hubs;

namespace WebApi.IntegrationTests.Project;

public class FileUploadWithHubIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public FileUploadWithHubIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => { builder.UseSetting("Environment", "Testing"); });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task UploadTextFile_ShouldTriggerFileReceivedAndFileSavedActivities_AndFileIsServed()
    {
        // Arrange: connect to SignalR hub
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "/chatHub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                // Use LongPolling transport for TestServer
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var activities = new ConcurrentQueue<(string Type, JsonElement Payload)>();
        hubConnection.On<Activity>("ReceiveActivity", activity =>
        {
            if (activity.Payload is JsonElement json) activities.Enqueue((activity.Type, json));
        });
        await hubConnection.StartAsync();

        // Act: upload a dummy text file
        var dummyBytes = new byte[] { 1, 2, 3, 4, 5 };
        var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(dummyBytes);
        byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(byteContent, "file", "test.txt");

        var response = await _client.PostAsync("/api/files/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Wait for at least two activities (file_received and file_saved)
        var start = DateTime.UtcNow;
        while (activities.Count < 2 && DateTime.UtcNow - start < TimeSpan.FromSeconds(5)) await Task.Delay(100);

        Assert.True(activities.Count >= 2, $"Expected at least 2 activities, got {activities.Count}");

        // Assert first activity is file_received
        Assert.True(activities.TryDequeue(out var first));
        Assert.Equal("file_received", first.Type);
        var fileName1 = first.Payload.GetProperty("fileName").GetString();
        Assert.False(string.IsNullOrEmpty(fileName1));

        // Assert second activity is file_saved
        Assert.True(activities.TryDequeue(out var second));
        Assert.Equal("file_saved", second.Type);
        var fileName2 = second.Payload.GetProperty("fileName").GetString();
        var url = second.Payload.GetProperty("url").GetString();
        Assert.Equal(fileName1, fileName2);
        Assert.Equal($"/files/{Uri.EscapeDataString(fileName2)}", url);

        // Assert the file is served correctly
        var fileResponse = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, fileResponse.StatusCode);
        var returned = await fileResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(dummyBytes, returned);

        await hubConnection.DisposeAsync();
    }
}