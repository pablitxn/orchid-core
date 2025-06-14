using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace WebApi.Middleware;

public class AuthorizationMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

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
        
        // First try to get token from cookie
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
        // For SignalR connections, check the access_token query parameter
        else if (context.Request.Query.TryGetValue("access_token", out var accessToken))
        {
            token = accessToken.ToString();
        }
        
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Authentication token missing");
            return;
        }

        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwtToken;
        try
        {
            // Decode the token (this doesn't validate the signature)
            jwtToken = handler.ReadJwtToken(token);
        }
        catch (Exception)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid token");
            return;
        }

        // Optional: Validate the token's signature, expiration, issuer, etc.
        // This part typically involves using TokenValidationParameters and handler.ValidateToken(...)

        // Extract the email claim
        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        if (emailClaim == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Email claim not found in token");
            return;
        }

        // Optionally, set the user principal in the context so that controllers can use it
        var claimsIdentity = new ClaimsIdentity(jwtToken.Claims, "Custom");
        context.User = new ClaimsPrincipal(claimsIdentity);

        // Continue with the next middleware in the pipeline
        await _next(context);
    }
}