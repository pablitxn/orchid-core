using System.Net.Http.Json;
using System.Text.Json;
using Integration.Shared.Fixtures;

namespace WebApi.IntegrationTests.Agents;

[Collection("Integration")]
public class AgentIntegrationTests
{
    private readonly HttpClient _client;

    public AgentIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(Skip = "Test requires auth/service refactor")]
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
