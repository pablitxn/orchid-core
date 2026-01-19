using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Integration.Shared.Fixtures;

/// <summary>
/// Test authentication handler that decodes user ID from base64-encoded bearer tokens.
/// Use <see cref="TokenHelper.GenerateToken"/> to create valid test tokens.
/// </summary>
#pragma warning disable CS0618 // ISystemClock is obsolete but required by AuthenticationHandler
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authHeader["Bearer ".Length..].Trim();

        try
        {
            var userIdBytes = Convert.FromBase64String(token);
            var userId = Encoding.UTF8.GetString(userIdBytes);

            if (Guid.TryParse(userId, out var parsedUserId))
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, parsedUserId.ToString()),
                    new Claim(ClaimTypes.Name, $"TestUser_{parsedUserId}")
                };

                var identity = new ClaimsIdentity(claims, SchemeName);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, SchemeName);

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }
        catch
        {
            // Invalid token format
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
    }
}
#pragma warning restore CS0618

/// <summary>
/// Helper methods for generating test authentication tokens.
/// </summary>
public static class TokenHelper
{
    /// <summary>
    /// Generates a base64-encoded token from a user ID for test authentication.
    /// </summary>
    public static string GenerateToken(Guid userId)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(userId.ToString()));
    }
}
