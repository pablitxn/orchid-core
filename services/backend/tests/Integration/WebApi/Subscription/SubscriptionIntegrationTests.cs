using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Integration.Shared.Fixtures;

namespace WebApi.IntegrationTests.Subscription;

[Collection("Integration")]
public class SubscriptionIntegrationTests
{
    private readonly HttpClient _client;

    public SubscriptionIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_Then_Get_Works()
    {
        var userId = Guid.NewGuid();
        var createRes = await _client.PostAsJsonAsync("/api/subscription", new { userId, credits = 10 });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);

        var getRes = await _client.GetAsync($"/api/subscription/{userId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var json = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("credits", out _));
    }
}
