using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Integration.Shared.Fixtures;

/// <summary>
/// Shared authentication handler for integration tests that uses a fixed user ID.
/// </summary>
public class GuidTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public GuidTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "Integration");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Integration");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
