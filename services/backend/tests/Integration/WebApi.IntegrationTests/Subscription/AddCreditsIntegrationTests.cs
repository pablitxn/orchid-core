using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApi.IntegrationTests.Subscription;

public class AddCreditsIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client =
        factory.WithWebHostBuilder(b => b.UseSetting("Environment", "Testing")).CreateClient();

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