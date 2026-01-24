using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApi.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireScopeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _requiredScope;

    public RequireScopeAttribute(string requiredScope)
    {
        _requiredScope = requiredScope;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Check if this is a service account token
        var tokenType = user.FindFirst("type")?.Value;
        if (tokenType != "service_account")
        {
            // Regular user tokens don't need scope validation
            return;
        }

        var scopes = GetScopes(user);

        if (!scopes.Contains(_requiredScope, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new ForbidResult();
        }
    }

    private static IReadOnlyCollection<string> GetScopes(ClaimsPrincipal user)
    {
        var scopes = new List<string>();

        scopes.AddRange(user.FindAll("scope").Select(c => c.Value));

        var scopesClaim = user.FindFirst("scopes")?.Value;
        if (!string.IsNullOrWhiteSpace(scopesClaim))
        {
            if (TryParseJsonScopes(scopesClaim, out var parsedScopes))
            {
                scopes.AddRange(parsedScopes);
            }
            else
            {
                scopes.AddRange(scopesClaim.Split(
                    new[] { ' ', ',' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        return scopes;
    }

    private static bool TryParseJsonScopes(string scopesClaim, out string[] scopes)
    {
        scopes = Array.Empty<string>();

        if (!scopesClaim.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            scopes = JsonSerializer.Deserialize<string[]>(scopesClaim) ?? Array.Empty<string>();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
