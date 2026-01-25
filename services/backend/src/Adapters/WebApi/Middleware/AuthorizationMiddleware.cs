using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace WebApi.Middleware;

public class AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<AuthorizationMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
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

        try
        {
            var jwtKey = configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("JWT configuration error");
                return;
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

            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            // Security: Check if token has been blacklisted (e.g., after logout)
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

            // Regular users require Email claim
            var emailClaim = principal.FindFirst(ClaimTypes.Email);
            if (emailClaim == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Email claim not found in token");
                return;
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
}
