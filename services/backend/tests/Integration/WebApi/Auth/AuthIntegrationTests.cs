using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Integration.Shared.Fixtures;

namespace WebApi.IntegrationTests.Auth;

[Collection("Integration")]
public class AuthIntegrationTests
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Then_Login_Works()
    {
        var email = $"user{Guid.NewGuid()}@example.com";
        var password = "TestPass1!"; // Valid password: min 8 chars, uppercase, lowercase, digit, special char
        var registerRes = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password, roles = new[] { "user" } });
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var json = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "invalid-email", roles = new[] { "User" } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_WithNonExistentRole_ReturnsBadRequest()
    {
        var email = $"user{Guid.NewGuid()}@example.com";
        var res = await _client.PostAsJsonAsync("/api/auth/register", new { email, roles = new[] { "NonRole" } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Must include password to avoid BadRequest for missing required fields
        var loginRes = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "noone@example.com", password = "TestPass1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginRes.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutRequiredFields_ReturnsBadRequest()
    {
        // Missing required fields should return BadRequest (validation error), not Unauthorized
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var res = await _client.PostAsync("/api/auth/login", content);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact(Skip = "Password reset flow cannot be tested in integration: token is not returned by endpoint for security (prevents email enumeration)")]
    public async Task Forgot_And_Reset_Password_Works()
    {
        // This test cannot work because:
        // 1. /api/auth/forgot-password does NOT return the token (security: prevents email enumeration)
        // 2. The token is only logged in development, not returned to client
        // Test the password reset flow via unit tests instead.
        var email = $"user{Guid.NewGuid()}@example.com";
        var password = "TestPass1!";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password, roles = new[] { "user" } });
        var forgotRes = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, forgotRes.StatusCode);
    }

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        var res = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
