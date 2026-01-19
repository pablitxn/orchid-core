using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Hubs;

namespace WebApi.IntegrationTests.Project;

public class FileUploadKnowledgeIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public FileUploadKnowledgeIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => { builder.UseSetting("Environment", "Testing"); });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task UploadTextFile_WithKnowledgeType_ShouldPersistDocumentType()
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "/chatHub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var activities = new ConcurrentQueue<(string Type, JsonElement Payload)>();
        hubConnection.On<Activity>("ReceiveActivity", activity =>
        {
            if (activity.Payload is JsonElement json) activities.Enqueue((activity.Type, json));
        });
        await hubConnection.StartAsync();

        var dummyBytes = new byte[] { 1, 2, 3, 4, 5 };
        var sessionId = Guid.NewGuid().ToString();
        var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(dummyBytes);
        byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(byteContent, "file", "test.txt");
        content.Add(new StringContent("knowledge"), "type");
        content.Add(new StringContent(sessionId), "sessionId");

        var response = await _client.PostAsync("/api/files/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var start = DateTime.UtcNow;
        while (activities.Count < 2 && DateTime.UtcNow - start < TimeSpan.FromSeconds(5)) await Task.Delay(100);

        Assert.True(activities.Count >= 2, $"Expected at least 2 activities, got {activities.Count}");

        Assert.True(activities.TryDequeue(out var first));
        Assert.Equal("file_received", first.Type);
        var fileName1 = first.Payload.GetProperty("fileName").GetString();
        Assert.False(string.IsNullOrEmpty(fileName1));

        Assert.True(activities.TryDequeue(out var second));
        Assert.Equal("file_saved", second.Type);
        var fileName2 = second.Payload.GetProperty("fileName").GetString();
        var url = second.Payload.GetProperty("url").GetString();
        Assert.Equal(fileName1, fileName2);
        Assert.Equal($"/api/files/{Uri.EscapeDataString(fileName2)}", url);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.FileName == fileName2);
        Assert.NotNull(doc);
        Assert.Equal(DocumentEnum.KnowledgeBase, doc!.Enum);

        await hubConnection.DisposeAsync();
    }
}