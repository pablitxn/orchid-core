using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace WebApi.Middleware;

/// <summary>
/// Custom JWT validation middleware that supports both Auth0 (RS256/JWKS) and Legacy (HMAC-SHA256) tokens.
/// Auth0 is enabled when Auth0:Domain configuration is set.
/// </summary>
public class AuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthorizationMiddleware> _logger;

    // JWKS configuration manager - cached and thread-safe
    private static IConfigurationManager<OpenIdConnectConfiguration>? _auth0ConfigManager;
    private static readonly object _configManagerLock = new();

    public AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Bypass CORS preflight requests (OPTIONS) - these don't carry auth tokens
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Bypass token validation only for integration tests
        var env = context.RequestServices.GetService<IWebHostEnvironment>();
        if (env != null && env.IsEnvironment("Testing"))
        {
            await _next(context);
            return;
        }

        // Check if the endpoint allows anonymous access
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            await _next(context);
            return;
        }

        // Health check endpoint should be accessible without authentication
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Skip middleware for SignalR hubs if user is already authenticated
        if ((context.Request.Path.StartsWithSegments("/chatHub") ||
             context.Request.Path.StartsWithSegments("/huddleHub")) &&
            context.User?.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // Check for token in cookie, Authorization header, or query string (for SignalR)
        string? token = null;

        // First try to get token from cookie (most secure)
        if (context.Request.Cookies.TryGetValue("token", out var cookieToken))
        {
            token = cookieToken;
        }
        // If no cookie, check Authorization header for Bearer token
        else if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authHeaderValue = authHeader.ToString();
            if (authHeaderValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeaderValue.Substring("Bearer ".Length).Trim();
            }
        }
        // For SignalR connections only, check the access_token query parameter
        // Note: This is less secure as tokens may appear in logs - use only for WebSocket upgrade
        else if (context.Request.Query.TryGetValue("access_token", out var accessToken))
        {
            var isSignalRPath = context.Request.Path.StartsWithSegments("/chatHub") ||
                               context.Request.Path.StartsWithSegments("/huddleHub");

            if (isSignalRPath)
            {
                token = accessToken.ToString();

                // Log security warning in development
                if (env?.IsDevelopment() == true)
                {
                    _logger.LogDebug(
                        "SignalR connection using query string token for {Path}. Consider using cookie-based auth in production.",
                        context.Request.Path);
                }
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Authentication token missing");
            return;
        }

        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var handler = new JwtSecurityTokenHandler();

        // Determine token type by peeking at issuer
        var auth0Domain = configuration["Auth0:Domain"];
        var auth0Enabled = !string.IsNullOrWhiteSpace(auth0Domain);
        var isAuth0Token = false;

        if (auth0Enabled && handler.CanReadToken(token))
        {
            try
            {
                var jwtToken = handler.ReadJwtToken(token);
                var issuer = jwtToken.Issuer;
                // Auth0 tokens have issuer like "https://{domain}/"
                if (issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    issuer.Contains(auth0Domain, StringComparison.OrdinalIgnoreCase))
                {
                    isAuth0Token = true;
                }
            }
            catch
            {
                // If we can't read the token, treat as legacy
            }
        }

        try
        {
            ClaimsPrincipal principal;

            if (isAuth0Token)
            {
                principal = await ValidateAuth0TokenAsync(token, configuration, handler, context.RequestAborted);
            }
            else
            {
                principal = ValidateLegacyToken(token, configuration, handler);
            }

            // Security: Check if token has been blacklisted (e.g., after logout)
            // Note: Auth0 tokens may not have JTI, blacklist check is skipped if absent
            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var blacklistService = context.RequestServices.GetService<IJwtBlacklistService>();
                if (blacklistService != null && await blacklistService.IsBlacklistedAsync(jti))
                {
                    var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    _logger.LogWarning(
                        "Blacklisted token used from IP {IpAddress} for path {Path}. JTI: {Jti}",
                        ipAddress, context.Request.Path, jti);

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Token has been revoked");
                    return;
                }
            }

            // Service accounts don't have Email claim - validate by type instead
            var tokenType = principal.FindFirst("type")?.Value;
            if (tokenType == "service_account")
            {
                context.User = principal;
                await _next(context);
                return;
            }

            // Auth0 tokens may have email in different claim types
            var emailClaim = principal.FindFirst(ClaimTypes.Email)
                          ?? principal.FindFirst("email")
                          ?? principal.FindFirst("https://orchid-core.com/email");

            if (emailClaim == null)
            {
                // For Auth0 tokens, email might be in the sub claim or might need to be fetched
                // For now, we'll allow Auth0 tokens without email if they have a valid sub claim
                if (isAuth0Token)
                {
                    var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
                                ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)
                                ?? principal.FindFirst("sub");
                    if (subClaim == null)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Subject claim not found in Auth0 token");
                        return;
                    }
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Email claim not found in token");
                    return;
                }
            }

            context.User = principal;
        }
        catch (SecurityTokenExpiredException ex)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogWarning(
                "Expired token used from IP {IpAddress} for path {Path}. Token expired at: {ExpirationTime}",
                ipAddress, context.Request.Path, ex.Expires);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Token has expired");
            return;
        }
        catch (SecurityTokenException ex)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogWarning(
                "Invalid token attempt from IP {IpAddress} for path {Path}. Error: {Error}",
                ipAddress, context.Request.Path, ex.Message);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid token");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Validates an Auth0 token using RS256 with JWKS endpoint.
    /// JWKS is automatically cached and refreshed.
    /// </summary>
    private async Task<ClaimsPrincipal> ValidateAuth0TokenAsync(
        string token,
        IConfiguration configuration,
        JwtSecurityTokenHandler handler,
        CancellationToken cancellationToken)
    {
        var auth0Domain = configuration["Auth0:Domain"]!;
        var auth0Audience = configuration["Auth0:Audience"];

        // Get or create the configuration manager (thread-safe, cached)
        var configManager = GetOrCreateAuth0ConfigManager(auth0Domain);

        // Fetch OpenID Connect configuration (JWKS is cached automatically with 1-hour refresh)
        var openIdConfig = await configManager.GetConfigurationAsync(cancellationToken);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://{auth0Domain}/",
            ValidateAudience = true,
            ValidAudience = auth0Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = openIdConfig.SigningKeys,
            // Map Auth0 claims to standard .NET claims
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = "permissions"
        };

        var principal = handler.ValidateToken(token, validationParameters, out _);

        // Map Auth0 claims to internal claims for consistency
        var identity = principal.Identity as ClaimsIdentity;
        if (identity != null)
        {
            // Ensure NameIdentifier is set from sub claim
            if (!identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
            {
                var subClaim = identity.FindFirst(JwtRegisteredClaimNames.Sub)
                            ?? identity.FindFirst("sub");
                if (subClaim != null)
                {
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subClaim.Value));
                }
            }

            // Map email claim if present
            var emailValue = identity.FindFirst("email")?.Value
                          ?? identity.FindFirst("https://orchid-core.com/email")?.Value;
            if (!string.IsNullOrEmpty(emailValue) && !identity.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                identity.AddClaim(new Claim(ClaimTypes.Email, emailValue));
            }

            // Map permissions to roles if present
            var permissions = identity.FindAll("permissions").ToList();
            foreach (var permission in permissions)
            {
                if (!identity.HasClaim(ClaimTypes.Role, permission.Value))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, permission.Value));
                }
            }
        }

        return principal;
    }

    /// <summary>
    /// Validates a legacy token using HMAC-SHA256.
    /// </summary>
    private static ClaimsPrincipal ValidateLegacyToken(
        string token,
        IConfiguration configuration,
        JwtSecurityTokenHandler handler)
    {
        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            throw new SecurityTokenException("JWT configuration error: Jwt:Key not configured");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        return handler.ValidateToken(token, validationParameters, out _);
    }

    /// <summary>
    /// Gets or creates the Auth0 configuration manager (JWKS caching).
    /// Thread-safe singleton per domain.
    /// </summary>
    private static IConfigurationManager<OpenIdConnectConfiguration> GetOrCreateAuth0ConfigManager(string auth0Domain)
    {
        if (_auth0ConfigManager != null)
        {
            return _auth0ConfigManager;
        }

        lock (_configManagerLock)
        {
            if (_auth0ConfigManager == null)
            {
                var metadataAddress = $"https://{auth0Domain}/.well-known/openid-configuration";
                _auth0ConfigManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = true });
            }
            return _auth0ConfigManager;
        }
    }
}
