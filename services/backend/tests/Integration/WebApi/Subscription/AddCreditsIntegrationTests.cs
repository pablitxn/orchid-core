using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Integration.Shared.Fixtures;

namespace WebApi.IntegrationTests.Subscription;

[Collection("Integration")]
public class AddCreditsIntegrationTests
{
    private readonly HttpClient _client;

    public AddCreditsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddCredits_IncrementsCredits()
    {
        var userId = Guid.NewGuid();
        await _client.PostAsJsonAsync("/api/subscription", new { userId, credits = 0 });

        var addRes = await _client.PostAsJsonAsync("/api/subscription/add", new { userId, amount = 5 });
        Assert.Equal(HttpStatusCode.OK, addRes.StatusCode);

        var getRes = await _client.GetAsync($"/api/subscription/{userId}");
        var json = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, json.GetProperty("credits").GetInt32());
    }
}
