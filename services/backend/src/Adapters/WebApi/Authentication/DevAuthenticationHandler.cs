using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace WebApi.Authentication;

/// <summary>
/// Dev-only authentication handler that always authenticates a local user.
/// Enabled via Auth:Disable=true.
/// </summary>
public sealed class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = configuration["Auth:DevUserId"] ?? "00000000-0000-0000-0000-000000000001";
        var userEmail = configuration["Auth:DevUserEmail"] ?? "admin@example.com";
        var userName = configuration["Auth:DevUserName"] ?? "dev-admin";
        var rolesValue = configuration["Auth:DevRoles"] ?? "admin,user";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, userEmail),
            new(ClaimTypes.Name, userName)
        };

        foreach (var role in rolesValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
