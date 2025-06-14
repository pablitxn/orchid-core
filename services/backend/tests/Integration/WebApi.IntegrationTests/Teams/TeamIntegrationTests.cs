using System.Net.Http.Json;
using System.Text.Json;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApi.IntegrationTests.Teams;

public class TeamIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TeamIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.UseSetting("Environment", "Testing")).CreateClient();
    }

    [Fact]
    public async Task CreateTeam_WithAgentRole_PersistsMapping()
    {
        var agentRes = await _client.PostAsJsonAsync("/api/agents", new { name = "Helper" });
        agentRes.EnsureSuccessStatusCode();
        var agentJson = await agentRes.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agentJson.GetProperty("id").GetGuid();

        var teamRes = await _client.PostAsJsonAsync("/api/teams", new
        {
            name = "MyTeam",
            description = "desc",
            policy = TeamInteractionPolicy.Open,
            agents = new[] { new { agentId, role = "leader", order = 1 } }
        });
        teamRes.EnsureSuccessStatusCode();

        var teams = await _client.GetFromJsonAsync<JsonElement[]>("/api/teams");
        var team = teams!.Single(t => t.GetProperty("name").GetString() == "MyTeam");
        var teamAgents = team.GetProperty("teamAgents").EnumerateArray().ToArray();
        Assert.Single(teamAgents);
        Assert.Equal("leader", teamAgents[0].GetProperty("role").GetString());
        Assert.Equal(agentId, teamAgents[0].GetProperty("agent").GetProperty("id").GetGuid());
    }
}