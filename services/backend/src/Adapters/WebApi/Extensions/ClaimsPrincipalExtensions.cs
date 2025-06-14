using System;
using System.Security.Claims;

namespace WebApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                       ?? principal.FindFirst("sub")?.Value
                       ?? principal.FindFirst("id")?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim))
        {
            throw new UnauthorizedAccessException("User ID not found in claims");
        }

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("Invalid user ID format");
    }

    public static string GetUserEmail(this ClaimsPrincipal principal)
    {
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UnauthorizedAccessException("User email not found in claims");
        }

        return email;
    }
}