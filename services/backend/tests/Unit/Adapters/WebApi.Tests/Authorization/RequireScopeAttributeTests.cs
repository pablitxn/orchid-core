using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using WebApi.Authorization;

namespace WebApi.Tests.Authorization;

public class RequireScopeAttributeTests
{
    private static AuthorizationFilterContext CreateContext(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public void OnAuthorization_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = CreateContext(user);
        var attribute = new RequireScopeAttribute("matrix:read");

        attribute.OnAuthorization(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void OnAuthorization_AllowsAuthenticatedUsers_WhenNotServiceAccount()
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            authenticationType: "Test");
        var context = CreateContext(new ClaimsPrincipal(identity));
        var attribute = new RequireScopeAttribute("matrix:read");

        attribute.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_AllowsServiceAccount_WhenScopePresent()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("type", "service_account"),
                new Claim("scopes", "[\"matrix:read\",\"matrix:write\"]")
            },
            authenticationType: "Test");
        var context = CreateContext(new ClaimsPrincipal(identity));
        var attribute = new RequireScopeAttribute("matrix:read");

        attribute.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_ForbidsServiceAccount_WhenScopeMissing()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("type", "service_account"),
                new Claim("scopes", "[\"matrix:read\"]")
            },
            authenticationType: "Test");
        var context = CreateContext(new ClaimsPrincipal(identity));
        var attribute = new RequireScopeAttribute("matrix:write");

        attribute.OnAuthorization(context);

        Assert.IsType<ForbidResult>(context.Result);
    }
}
