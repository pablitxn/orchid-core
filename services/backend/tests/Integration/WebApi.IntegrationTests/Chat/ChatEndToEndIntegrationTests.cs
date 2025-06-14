using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WebApi.IntegrationTests.Chat;

public class ChatEndToEndIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatEndToEndIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributedCache>();
                services.AddSingleton<IDistributedCache>(
                    new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
                services.RemoveAll<IChatCompletionPort>();
                services.AddSingleton<IChatCompletionPort>(new FakeChatPort());
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Integration";
                    options.DefaultChallengeScheme = "Integration";
                }).AddScheme<AuthenticationSchemeOptions, GuidTestAuthHandler>("Integration", _ => { });
            });
        });
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

    private sealed class FakeChatPort : IChatCompletionPort
    {
        public Task<string> CompleteAsync(IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("pong");
        }

        public Task<string> CompleteWithAgentAsync(IEnumerable<ChatMessage> messages, Guid agentId, Guid? userId = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<string> CompleteWithAgentAsync(IEnumerable<ChatMessage> messages, Guid agentId,
            CancellationToken cancellationToken = default,
            Guid? userId = null)
        {
            return Task.FromResult("pong");
        }

        public Task<string> CompleteWithAgentAsync(IEnumerable<ChatMessage> messages, Guid agentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("pong");
        }

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "pong";
            await Task.Yield();
        }
    }
}

internal class GuidTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public GuidTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "Integration");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Integration");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}