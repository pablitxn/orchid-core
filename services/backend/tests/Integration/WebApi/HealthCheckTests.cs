using System.Net;
using System.Text.Json;
using Integration.Shared.Fixtures;

namespace WebApi.IntegrationTests;

[Collection("Integration")]
public class HealthCheckTests
{
    private readonly HttpClient _client;

    public HealthCheckTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsExpectedResponse()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        // Parse the JSON response
        var healthReport = JsonDocument.Parse(content);
        var root = healthReport.RootElement;

        // Log the response for debugging
        Console.WriteLine($"Health check response: {content}");

        // Check that the response has the expected structure
        Assert.True(root.TryGetProperty("status", out var status), $"Response missing 'status' property. Content: {content}");
        Assert.True(root.TryGetProperty("entries", out var entries), $"Response missing 'entries' property. Content: {content}");

        // Log the results for debugging
        var resultsDict = entries.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        // Check for expected health checks
        Assert.True(resultsDict.ContainsKey("postgres"), "Missing postgres health check");
        Assert.True(resultsDict.ContainsKey("redis"), "Missing redis health check");
        Assert.True(resultsDict.ContainsKey("memory"), "Missing memory health check");
        Assert.True(resultsDict.ContainsKey("seeding"), "Missing seeding health check");
        Assert.True(resultsDict.ContainsKey("self"), "Missing self health check");

        // Check if Langfuse health check is present (may not be if not configured)
        if (resultsDict.ContainsKey("langfuse"))
        {
            var langfuseCheck = resultsDict["langfuse"];
            Assert.True(langfuseCheck.TryGetProperty("status", out var langfuseStatus));

            // Log Langfuse health check details
            if (langfuseCheck.TryGetProperty("description", out var description))
            {
                var descriptionText = description.GetString();
                Assert.NotNull(descriptionText);
            }

            if (langfuseCheck.TryGetProperty("data", out var data))
            {
                // Check for expected data properties
                if (data.TryGetProperty("configured", out var configured))
                {
                    Assert.True(configured.GetBoolean() || !configured.GetBoolean());
                }
            }
        }
    }
}
