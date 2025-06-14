using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WebApi.IntegrationTests.ChatHub;

public class StreamingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StreamingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributedCache>();
                services.AddSingleton<IDistributedCache>(
                    new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Integration";
                    options.DefaultChallengeScheme = "Integration";
                }).AddScheme<AuthenticationSchemeOptions, GuidTestAuthHandler>("Integration", _ => { });
                services.RemoveAll<IChatCompletionPort>();
                services.AddSingleton<IChatCompletionPort>(new FakeChatCompletionPort());
            });
        });
    }

    [Fact]
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

    private sealed class FakeChatCompletionPort : IChatCompletionPort
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
            yield return "po";
            await Task.Yield();
            yield return "ng";
        }
    }
}

internal class GuidTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public GuidTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
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