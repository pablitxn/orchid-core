using System.Net.Http.Json;
using System.Text.Json;
using Application.Interfaces;
using Integration.Shared.Fixtures;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace WebApi.IntegrationTests.Chat;

[Collection("Integration")]
public class ChatEndToEndIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ChatEndToEndIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullChatFlow_Works()
    {
        var client = _factory.CreateClient();
        var userId = GuidTestAuthHandler.UserId;
        var sessionId = Guid.NewGuid().ToString();
        var createRes = await client.PostAsJsonAsync("/api/chat-sessions", new { userId, sessionId, title = "e2e" });
        createRes.EnsureSuccessStatusCode();

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/chatHub"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                opts.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        await connection.StartAsync();

        var textChunks = new List<string>();
        await foreach (var chunk in connection.StreamAsync<string>("StreamMessage", sessionId, "ping", false))
            textChunks.Add(chunk);

        Assert.Equal("pong", string.Concat(textChunks));

        var fileChunks = new List<string>();
        var payload = JsonSerializer.Serialize(new { type = "file", name = "demo.txt" });
        await foreach (var chunk in connection.StreamAsync<string>("StreamMessage", sessionId, payload, false))
            fileChunks.Add(chunk);

        Assert.Equal("File stored and ready.", string.Concat(fileChunks));

        var history = await connection.InvokeAsync<IReadOnlyList<ChatMessage>>("GetHistory", sessionId);
        Assert.Contains(history, m => m.Content == "pong");
        Assert.Contains(history, m => m.Content == "File stored and ready.");

        await connection.DisposeAsync();
    }
}
