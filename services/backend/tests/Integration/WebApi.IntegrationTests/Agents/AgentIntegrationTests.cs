using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApi.IntegrationTests.Agents;

public class AgentIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AgentIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.UseSetting("Environment", "Testing")).CreateClient();
    }

    [Fact]
    public async Task CreateAgent_WithPlugin_AssignsPlugin()
    {
        var pluginRes = await _client.PostAsJsonAsync("/api/plugins",
            new { name = "Excel", description = "d", sourceUrl = "http://x" });
        pluginRes.EnsureSuccessStatusCode();
        var pluginJson = await pluginRes.Content.ReadFromJsonAsync<JsonElement>();
        var pluginId = pluginJson.GetProperty("id").GetGuid();

        var agentRes =
            await _client.PostAsJsonAsync("/api/agents", new { name = "Bot", pluginIds = new[] { pluginId } });
        agentRes.EnsureSuccessStatusCode();

        var agents = await _client.GetFromJsonAsync<JsonElement[]>("/api/agents");
        var created = agents!.Single(a => a.GetProperty("name").GetString() == "Bot");
        var ids = created.GetProperty("pluginIds").EnumerateArray().Select(v => v.GetGuid()).ToArray();
        Assert.Contains(pluginId, ids);
    }
}