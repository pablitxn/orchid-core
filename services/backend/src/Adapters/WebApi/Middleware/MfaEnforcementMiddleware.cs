using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Orchid.Infrastructure.Auth;

namespace WebApi.Middleware;

/// <summary>
/// Middleware that enforces MFA requirements for users with specified roles.
/// Must run after AuthorizationMiddleware to have access to the authenticated user.
/// </summary>
public class MfaEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MfaEnforcementMiddleware> _logger;

    public MfaEnforcementMiddleware(RequestDelegate next, ILogger<MfaEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<MfaSettings> mfaSettings, IUserRepository userRepository)
    {
        var settings = mfaSettings.Value;

        // Skip if MFA enforcement is disabled
        if (!settings.Enabled)
        {
            await _next(context);
            return;
        }

        // Skip for anonymous endpoints
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            await _next(context);
            return;
        }

        // Skip if user is not authenticated
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Skip for certain paths that should always be accessible
        if (ShouldSkipMfaCheck(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Get user's roles
        var userRoles = context.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value.ToLowerInvariant())
            .ToHashSet();

        // Check if any of the user's roles require MFA
        var requiresMfa = settings.RequiredForRoles
            .Any(requiredRole => userRoles.Contains(requiredRole.ToLowerInvariant()));

        if (!requiresMfa)
        {
            await _next(context);
            return;
        }

        // User has a role that requires MFA - check if they have MFA enabled
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            // Can't determine user ID - allow through (will be handled by other auth)
            await _next(context);
            return;
        }

        var user = await userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            // User not found in database - might be a new Auth0 user
            // Let the request through; the sync process will handle them
            await _next(context);
            return;
        }

        if (!user.MfaEnabled)
        {
            _logger.LogWarning(
                "MFA required but not enabled for user {UserId} with roles {Roles}",
                userId, string.Join(", ", userRoles));

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "mfa_required",
                message = "Multi-factor authentication is required for your account. Please enable MFA in your security settings.",
                requiredForRoles = settings.RequiredForRoles
            });
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Returns true if the path should skip MFA enforcement.
    /// These are paths needed for the user to set up MFA.
    /// </summary>
    private static bool ShouldSkipMfaCheck(PathString path)
    {
        // Allow access to health checks
        if (path.StartsWithSegments("/health"))
            return true;

        // Allow access to MFA status endpoint (user needs to check their status)
        if (path.StartsWithSegments("/api/user/mfa"))
            return true;

        // Allow access to webhooks
        if (path.StartsWithSegments("/api/webhooks"))
            return true;

        return false;
    }
}
