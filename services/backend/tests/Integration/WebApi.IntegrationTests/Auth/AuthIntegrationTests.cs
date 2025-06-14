using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApi.IntegrationTests.Auth;

public class AuthIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client =
        factory.WithWebHostBuilder(b => b.UseSetting("Environment", "Testing")).CreateClient();

    [Fact]
    public async Task Register_Then_Login_Works()
    {
        var email = $"user{Guid.NewGuid()}@example.com";
        var registerRes = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "pass", roles = new[] { "user" } });
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "pass" });
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
        var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new { email = "noone@example.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginRes.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutRequiredFields_ReturnsUnauthorized()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var res = await _client.PostAsync("/api/auth/login", content);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Forgot_And_Reset_Password_Works()
    {
        var email = $"user{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "old", roles = new[] { "user" } });
        var forgotRes = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, forgotRes.StatusCode);
        var tokenJson = await forgotRes.Content.ReadFromJsonAsync<JsonElement>();
        var token = tokenJson.GetProperty("token").GetString();
        var resetRes =
            await _client.PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword = "new" });
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);
        var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "new" });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
    }

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        var res = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}