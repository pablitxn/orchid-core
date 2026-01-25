using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Integration.Shared.Fixtures;

/// <summary>
/// Authentication handler for testing service account endpoints.
/// Simulates a service account with configurable scopes.
/// </summary>
public class ServiceAccountTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public static string[] Scopes { get; set; } = Array.Empty<string>();

    public ServiceAccountTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new("type", "service_account"),
            new("client_id", "test-service-account")
        };

        if (Scopes.Length > 0)
        {
            claims.Add(new Claim("scopes", System.Text.Json.JsonSerializer.Serialize(Scopes)));
        }

        var identity = new ClaimsIdentity(claims, "ServiceAccount");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "ServiceAccount");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
