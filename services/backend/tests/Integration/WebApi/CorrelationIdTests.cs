using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace WebApi.IntegrationTests;

/// <summary>
///     Integration tests for the Correlation ID middleware.
///     Verifies that correlation IDs are properly generated, propagated, and returned in responses.
/// </summary>
public class CorrelationIdTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    private readonly HttpClient _client =
        factory.WithWebHostBuilder(b => b.UseSetting("Environment", "Testing")).CreateClient();

    [Fact]
    public async Task Request_WithoutCorrelationId_ShouldGenerateAndReturnOne()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.Contains(CorrelationIdHeader),
            "Response should contain X-Correlation-ID header");

        var correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
        Assert.Equal(12, correlationId.Length); // Short format: abc123def456
    }

    [Fact]
    public async Task Request_WithCorrelationId_ShouldUseProvidedId()
    {
        // Arrange
        var providedCorrelationId = "test-correlation-id-123";
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdHeader, providedCorrelationId);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.Contains(CorrelationIdHeader),
            "Response should contain X-Correlation-ID header");

        var returnedCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.Equal(providedCorrelationId, returnedCorrelationId);
    }

    [Fact]
    public async Task MultipleRequests_ShouldHaveUniqueCorrelationIds()
    {
        // Act
        var response1 = await _client.GetAsync("/health");
        var response2 = await _client.GetAsync("/health");
        var response3 = await _client.GetAsync("/health");

        // Assert
        var correlationId1 = response1.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        var correlationId2 = response2.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        var correlationId3 = response3.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();

        Assert.NotNull(correlationId1);
        Assert.NotNull(correlationId2);
        Assert.NotNull(correlationId3);

        // All correlation IDs should be unique
        Assert.NotEqual(correlationId1, correlationId2);
        Assert.NotEqual(correlationId2, correlationId3);
        Assert.NotEqual(correlationId1, correlationId3);
    }

    [Fact]
    public async Task Request_WithEmptyCorrelationId_ShouldGenerateNewOne()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdHeader, "");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
        Assert.Equal(12, correlationId.Length); // Should generate a new short-format ID
    }

    [Fact]
    public async Task Request_WithWhitespaceCorrelationId_ShouldGenerateNewOne()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdHeader, "   ");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.NotNull(correlationId);
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }
}
