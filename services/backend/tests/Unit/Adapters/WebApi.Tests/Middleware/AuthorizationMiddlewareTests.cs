using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using WebApi.Middleware;

namespace WebApi.Tests.Middleware;

public class AuthorizationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsUserPrincipal_WhenCookieContainsValidToken()
    {
        var claims = new[] { new Claim(ClaimTypes.Email, "test@example.com") };
        var token = new JwtSecurityToken("iss", "aud", claims);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var context = new DefaultHttpContext();
        context.Request.Headers.Add("Cookie", $"token={tokenString}");

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.IsEnvironment("Testing")).Returns(false);

        var services = new ServiceCollection();
        services.AddSingleton(envMock.Object);
        context.RequestServices = services.BuildServiceProvider();

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        Assert.Equal("test@example.com", context.User.FindFirst(ClaimTypes.Email)?.Value);
    }
}
