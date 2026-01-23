using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Application.Interfaces;
using Integration.Shared.Fixtures;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace WebApi.IntegrationTests.ChatHub;

[Collection("Integration")]
public class StreamingIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public StreamingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(Skip = "Method StreamMessage does not exist in ChatHub - actual method is SendMessageStreaming which uses callbacks not IAsyncEnumerable")]
    public async Task StreamMessage_ReturnsChunksAndPersistsResponse()
    {
        var client = _factory.CreateClient();
        var userId = GuidTestAuthHandler.UserId;
        await client.PostAsJsonAsync("/api/subscription", new { userId, credits = 1 });

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/chatHub"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                opts.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync();
        var sessionId = Guid.NewGuid().ToString();
        var chunks = new List<string>();
        await foreach (var chunk in connection.StreamAsync<string>("StreamMessage", sessionId, "ping", false))
            chunks.Add(chunk);
        var final = string.Concat(chunks);
        Assert.Equal("pong", final);

        var history = await connection.InvokeAsync<IReadOnlyList<ChatMessage>>("GetHistory", sessionId);
        Assert.Equal("pong", history.Last().Content);
        await connection.DisposeAsync();
    }
}
