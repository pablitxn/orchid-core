using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Integration.Shared.Fixtures;

namespace WebApi.IntegrationTests.Subscription;

[Collection("Integration")]
public class ConsumeCreditsIntegrationTests
{
    private readonly HttpClient _client;

    public ConsumeCreditsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateUserWithSubscriptionAsync(int credits)
    {
        // Register a new user
        var email = $"consume-test-{Guid.NewGuid()}@example.com";
        var password = "TestPass1!";
        var registerRes = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password, roles = new[] { "user" } });
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        var registerJson = await registerRes.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registerJson.GetProperty("id").GetGuid();

        // Create subscription for the user
        var createRes = await _client.PostAsJsonAsync("/api/subscription", new { userId, credits });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);

        return userId;
    }

    [Fact]
    public async Task ConsumeCredits_DeductsCredits()
    {
        var userId = await CreateUserWithSubscriptionAsync(5);

        var consumeRes = await _client.PostAsJsonAsync(
            "/api/subscription/consume",
            new { userId, amount = 2 });
        Assert.Equal(HttpStatusCode.OK, consumeRes.StatusCode);

        var getRes = await _client.GetAsync($"/api/subscription/{userId}");
        var json = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, json.GetProperty("credits").GetInt32());
    }

    [Fact]
    public async Task ConsumeCredits_InsufficientBalance_ReturnsBadRequest()
    {
        var userId = await CreateUserWithSubscriptionAsync(1);

        var res = await _client.PostAsJsonAsync(
            "/api/subscription/consume",
            new { userId, amount = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var json = await _client.GetFromJsonAsync<JsonElement>($"/api/subscription/{userId}");
        Assert.Equal(1, json.GetProperty("credits").GetInt32());
    }

    [Fact]
    public async Task ConsumeCredits_NoSubscription_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var res = await _client.PostAsJsonAsync(
            "/api/subscription/consume",
            new { userId, amount = 1 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
