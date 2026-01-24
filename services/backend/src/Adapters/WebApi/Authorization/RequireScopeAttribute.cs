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

        // Get all scope claims
        var scopes = user.FindAll("scope").Select(c => c.Value).ToArray();

        if (!scopes.Contains(_requiredScope))
        {
            context.Result = new ForbidResult();
        }
    }
}
