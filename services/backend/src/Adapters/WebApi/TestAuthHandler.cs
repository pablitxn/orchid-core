using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

// Adapter class to bridge TimeProvider to ISystemClock.
[Obsolete]
public class TimeProviderAdapter(TimeProvider timeProvider) : ISystemClock
{
    private readonly TimeProvider _timeProvider = timeProvider;

    // Returns the current UTC time from the wrapped TimeProvider.
    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();
}

[Obsolete]
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TimeProvider timeProvider
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder, new TimeProviderAdapter(timeProvider))
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Create a fake user with claims.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}