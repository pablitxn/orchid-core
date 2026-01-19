using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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

namespace WebApi.IntegrationTests.ChatHub;

public class CreditConsumptionIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CreditConsumptionIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");
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
            });
        });
    }

    [Fact]
    public async Task SendMessage_DeductsOneCredit()
    {
        var client = _factory.CreateClient();
        var userId = GuidTestAuthHandler.UserId;
        // Create subscription with initial credits
        var createResponse = await client.PostAsJsonAsync("/api/subscription", new { userId, credits = 2 });
        Assert.True(createResponse.IsSuccessStatusCode,
            $"Subscription creation failed with status code {createResponse.StatusCode}");

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/chatHub"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                opts.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        // Start SignalR connection and assert success
        try
        {
            await connection.StartAsync();
        }
        catch (Exception ex)
        {
            Assert.False(true, $"Failed to start SignalR connection: {ex}");
        }

        var sessionId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<(string Sender, string Content)>();
        connection.On<string, string>("ReceiveMessage", (sender, content) => { tcs.TrySetResult((sender, content)); });

        try
        {
            await connection.InvokeAsync("SendMessage", sessionId, "hola", false);
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completedTask != tcs.Task) Assert.False(true, "Did not receive confirmation message from hub.");

            var (sender, content) = tcs.Task.Result;
            Assert.Equal("bot", sender);
            Assert.False(string.IsNullOrWhiteSpace(content));
        }
        catch (Exception ex)
        {
            Assert.False(true, $"Exception thrown during SendMessage: {ex}");
        }
        finally
        {
            await connection.DisposeAsync();
        }

        // Verify one credit was deducted
        var getResponse = await client.GetAsync($"/api/subscription/{userId}");
        Assert.True(getResponse.IsSuccessStatusCode,
            $"Fetching subscription failed with status code {getResponse.StatusCode}");
        var json = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("credits").GetInt32());
    }

    [Fact]
    public async Task SendMessage_PerTokenBilling_DeductsBasedOnTokens()
    {
        var client = _factory.CreateClient();
        var userId = GuidTestAuthHandler.UserId;

        // Reset credits to known amount
        var existing = await client.GetFromJsonAsync<JsonElement>($"/api/subscription/{userId}");
        var current = existing.GetProperty("credits").GetInt32();
        if (current > 0) await client.PostAsJsonAsync("/api/subscription/consume", new { userId, amount = current });

        await client.PostAsJsonAsync("/api/subscription/add", new { userId, amount = 5 });

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/chatHub"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                opts.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync();
        var sessionId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<(string, string)>();
        connection.On<string, string>("ReceiveMessage", (s, c) => tcs.TrySetResult((s, c)));

        var text = new string('a', 1000);
        await connection.InvokeAsync("SendMessage", sessionId, text, true);
        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await connection.DisposeAsync();

        var resp = await client.GetFromJsonAsync<JsonElement>($"/api/subscription/{userId}");
        Assert.Equal(2, resp.GetProperty("credits").GetInt32());
    }

    [Fact]
    public async Task SendMessage_WithZeroCredits_ReturnsInsufficientCreditsAndDoesNotDeduct()
    {
        var client = _factory.CreateClient();
        var userId = GuidTestAuthHandler.UserId;

        // Consume any existing credits to reach zero balance
        var existing = await client.GetFromJsonAsync<JsonElement>($"/api/subscription/{userId}");
        var currentCredits = existing.GetProperty("credits").GetInt32();
        if (currentCredits > 0)
        {
            var consumeResponse =
                await client.PostAsJsonAsync("/api/subscription/consume", new { userId, amount = currentCredits });
            Assert.True(consumeResponse.IsSuccessStatusCode,
                $"Clearing credits failed with status code {consumeResponse.StatusCode}");
        }

        // Verify credits are zero
        var zeroCredits = await client.GetFromJsonAsync<JsonElement>($"/api/subscription/{userId}");
        Assert.Equal(0, zeroCredits.GetProperty("credits").GetInt32());

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/chatHub"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                opts.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var tcs = new TaskCompletionSource<string>();
        connection.On<string, string>("ReceiveMessage", (sender, content) =>
        {
            if (sender == "bot")
                tcs.TrySetResult(content);
        });

        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync("SendMessage", Guid.NewGuid().ToString(), "test", false);
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != tcs.Task) Assert.False(true, "Did not receive Insufficient credits message from hub.");

            var content = tcs.Task.Result;
            Assert.Equal("Insufficient credits.", content);
        }
        catch (Exception ex)
        {
            Assert.False(true, $"Exception thrown during SendMessage with zero credits: {ex}");
        }
        finally
        {
            await connection.DisposeAsync();
        }

        // Verify credits remain zero
        var final = await client.GetFromJsonAsync<JsonElement>($"/api/subscription/{userId}");
        Assert.Equal(0, final.GetProperty("credits").GetInt32());
    }

    [Fact]
    public async Task SendMessage_WithoutActiveSubscription_ReturnsSubscriptionNotFoundAndDoesNotDeduct()
    {
        var client = _factory.CreateClient();
        var userId = GuidTestAuthHandler.UserId;

        // Ensure no active subscription by attempting to consume any existing credits
        var getResp = await client.GetAsync($"/api/subscription/{userId}");
        if (getResp.IsSuccessStatusCode)
        {
            var existing = await getResp.Content.ReadFromJsonAsync<JsonElement>();
            var credits = existing.GetProperty("credits").GetInt32();
            if (credits > 0)
            {
                var consumeResp =
                    await client.PostAsJsonAsync("/api/subscription/consume", new { userId, amount = credits });
                Assert.True(consumeResp.IsSuccessStatusCode,
                    $"Clearing credits failed with status code {consumeResp.StatusCode}");
            }
        }

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/chatHub"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                opts.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var tcs = new TaskCompletionSource<string>();
        connection.On<string, string>("ReceiveMessage", (sender, content) =>
        {
            if (sender == "bot")
                tcs.TrySetResult(content);
        });

        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync("SendMessage", Guid.NewGuid().ToString(), "test", false);
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != tcs.Task) Assert.False(true, "Did not receive Subscription not found message from hub.");

            var content = tcs.Task.Result;
            Assert.Equal("Subscription not found.", content);
        }
        catch (Exception ex)
        {
            Assert.False(true, $"Exception thrown during SendMessage without subscription: {ex}");
        }
        finally
        {
            await connection.DisposeAsync();
        }

        // Verify subscription endpoint returns NotFound
        var finalResp = await client.GetAsync($"/api/subscription/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, finalResp.StatusCode);
    }
}

internal class GuidTestAuthHandler2(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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