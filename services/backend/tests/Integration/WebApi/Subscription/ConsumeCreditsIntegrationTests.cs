using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApi.IntegrationTests.Subscription;

public class ConsumeCreditsIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client =
        factory.WithWebHostBuilder(b => b.UseSetting("Environment", "Testing")).CreateClient();

    [Fact]
    public async Task ConsumeCredits_DeductsCredits()
    {
        var userId = Guid.NewGuid();
        await _client.PostAsJsonAsync("/api/subscription", new { userId, credits = 5 });

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
        var userId = Guid.NewGuid();
        await _client.PostAsJsonAsync("/api/subscription", new { userId, credits = 1 });

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