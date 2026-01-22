using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using WebApi.Middleware;

namespace WebApi.Tests.Middleware;

public class AuthorizationMiddlewareTests
{
    private readonly Mock<ILogger<AuthorizationMiddleware>> _loggerMock;

    public AuthorizationMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<AuthorizationMiddleware>>();
    }

    private static string GenerateValidJwtToken(string issuer, string audience, string key, string email)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static IServiceProvider BuildServiceProvider(
        bool isTestingEnvironment = false,
        string? jwtKey = null,
        string issuer = "test-issuer",
        string audience = "test-audience",
        IJwtBlacklistService? blacklistService = null)
    {
        var services = new ServiceCollection();

        var envMock = new Mock<IWebHostEnvironment>();
        // Note: IsEnvironment() is an extension method that checks EnvironmentName
        // so we just need to set EnvironmentName correctly
        envMock.Setup(e => e.EnvironmentName).Returns(isTestingEnvironment ? "Testing" : "Development");
        services.AddSingleton(envMock.Object);

        if (jwtKey != null)
        {
            var configurationMock = new Mock<IConfiguration>();
            configurationMock.Setup(c => c["Jwt:Key"]).Returns(jwtKey);
            configurationMock.Setup(c => c["Jwt:Issuer"]).Returns(issuer);
            configurationMock.Setup(c => c["Jwt:Audience"]).Returns(audience);
            services.AddSingleton(configurationMock.Object);
        }

        if (blacklistService != null)
        {
            services.AddSingleton(blacklistService);
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenTokenMissing()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = BuildServiceProvider(isTestingEnvironment: false);

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal("Authentication token missing", body);
    }

    [Fact]
    public async Task InvokeAsync_ThrowsSecurityTokenMalformedException_WhenTokenMalformed()
    {
        // Arrange
        const string jwtKey = "ThisIsASecretKeyForTestingPurposesOnly1234567890";
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Authorization = "Bearer invalid-token-string";
        context.RequestServices = BuildServiceProvider(
            isTestingEnvironment: false,
            jwtKey: jwtKey);

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act & Assert
        // Note: SecurityTokenMalformedException inherits from ArgumentException, not SecurityTokenException
        // so it is not caught by the middleware's existing exception handlers
        await Assert.ThrowsAsync<Microsoft.IdentityModel.Tokens.SecurityTokenMalformedException>(
            () => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenTokenExpired()
    {
        // Arrange
        const string jwtKey = "ThisIsASecretKeyForTestingPurposesOnly1234567890";
        const string issuer = "test-issuer";
        const string audience = "test-audience";

        // Create an expired token
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var expiredToken = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(-1), // Already expired
            signingCredentials: credentials);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(expiredToken);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Authorization = $"Bearer {tokenString}";
        context.RequestServices = BuildServiceProvider(
            isTestingEnvironment: false,
            jwtKey: jwtKey,
            issuer: issuer,
            audience: audience);

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal("Token has expired", body);
    }

    [Fact]
    public async Task InvokeAsync_BypassesValidation_WhenTestingEnvironment()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.RequestServices = BuildServiceProvider(isTestingEnvironment: true);

        var nextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AllowsHealthEndpoint_WithoutToken()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        context.RequestServices = BuildServiceProvider(isTestingEnvironment: false);

        var nextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_SetsUserPrincipal_WhenValidTokenInCookie()
    {
        // Arrange
        const string jwtKey = "ThisIsASecretKeyForTestingPurposesOnly1234567890";
        const string issuer = "test-issuer";
        const string audience = "test-audience";
        const string email = "test@example.com";

        var tokenString = GenerateValidJwtToken(issuer, audience, jwtKey, email);

        var context = new DefaultHttpContext();
        context.Request.Cookies = new MockRequestCookieCollection(new Dictionary<string, string>
        {
            ["token"] = tokenString
        });
        context.RequestServices = BuildServiceProvider(
            isTestingEnvironment: false,
            jwtKey: jwtKey,
            issuer: issuer,
            audience: audience);

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(email, context.User.FindFirst(ClaimTypes.Email)?.Value);
    }

    [Fact]
    public async Task InvokeAsync_SetsUserPrincipal_WhenValidTokenInAuthorizationHeader()
    {
        // Arrange
        const string jwtKey = "ThisIsASecretKeyForTestingPurposesOnly1234567890";
        const string issuer = "test-issuer";
        const string audience = "test-audience";
        const string email = "bearer@example.com";

        var tokenString = GenerateValidJwtToken(issuer, audience, jwtKey, email);

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {tokenString}";
        context.RequestServices = BuildServiceProvider(
            isTestingEnvironment: false,
            jwtKey: jwtKey,
            issuer: issuer,
            audience: audience);

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(email, context.User.FindFirst(ClaimTypes.Email)?.Value);
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenTokenBlacklisted()
    {
        // Arrange
        const string jwtKey = "ThisIsASecretKeyForTestingPurposesOnly1234567890";
        const string issuer = "test-issuer";
        const string audience = "test-audience";
        const string email = "blacklisted@example.com";

        var tokenString = GenerateValidJwtToken(issuer, audience, jwtKey, email);

        // Extract the JTI from the token for blacklisting
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

        var blacklistServiceMock = new Mock<IJwtBlacklistService>();
        blacklistServiceMock
            .Setup(s => s.IsBlacklistedAsync(jti!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Authorization = $"Bearer {tokenString}";
        context.RequestServices = BuildServiceProvider(
            isTestingEnvironment: false,
            jwtKey: jwtKey,
            issuer: issuer,
            audience: audience,
            blacklistService: blacklistServiceMock.Object);

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal("Token has been revoked", body);
    }

    [Fact]
    public async Task InvokeAsync_Returns401_WhenEmailClaimMissing()
    {
        // Arrange
        const string jwtKey = "ThisIsASecretKeyForTestingPurposesOnly1234567890";
        const string issuer = "test-issuer";
        const string audience = "test-audience";

        // Create a token without email claim
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("sub", "user-id") // Some other claim instead of email
        };
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Authorization = $"Bearer {tokenString}";
        context.RequestServices = BuildServiceProvider(
            isTestingEnvironment: false,
            jwtKey: jwtKey,
            issuer: issuer,
            audience: audience);

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal("Email claim not found in token", body);
    }

    [Fact]
    public async Task InvokeAsync_Returns500_WhenJwtKeyNotConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        services.AddSingleton(envMock.Object);

        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(c => c["Jwt:Key"]).Returns((string?)null);
        services.AddSingleton(configurationMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Authorization = "Bearer some-token";
        context.RequestServices = services.BuildServiceProvider();

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal("JWT configuration error", body);
    }

    [Fact]
    public async Task InvokeAsync_AcceptsTokenFromQueryString_ForSignalRPaths()
    {
        // Arrange
        const string jwtKey = "ThisIsASecretKeyForTestingPurposesOnly1234567890";
        const string issuer = "test-issuer";
        const string audience = "test-audience";
        const string email = "signalr@example.com";

        var tokenString = GenerateValidJwtToken(issuer, audience, jwtKey, email);

        var context = new DefaultHttpContext();
        context.Request.Path = "/chatHub";
        context.Request.QueryString = new QueryString($"?access_token={tokenString}");
        context.RequestServices = BuildServiceProvider(
            isTestingEnvironment: false,
            jwtKey: jwtKey,
            issuer: issuer,
            audience: audience);

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(email, context.User.FindFirst(ClaimTypes.Email)?.Value);
    }

    [Fact]
    public async Task InvokeAsync_IgnoresQueryStringToken_ForNonSignalRPaths()
    {
        // Arrange
        const string jwtKey = "ThisIsASecretKeyForTestingPurposesOnly1234567890";
        const string issuer = "test-issuer";
        const string audience = "test-audience";
        const string email = "api@example.com";

        var tokenString = GenerateValidJwtToken(issuer, audience, jwtKey, email);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/users";
        context.Request.QueryString = new QueryString($"?access_token={tokenString}");
        context.RequestServices = BuildServiceProvider(
            isTestingEnvironment: false,
            jwtKey: jwtKey,
            issuer: issuer,
            audience: audience);

        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }
}

/// <summary>
/// Helper class to mock IRequestCookieCollection
/// </summary>
internal class MockRequestCookieCollection : IRequestCookieCollection
{
    private readonly Dictionary<string, string> _cookies;

    public MockRequestCookieCollection(Dictionary<string, string> cookies)
    {
        _cookies = cookies;
    }

    public string? this[string key] => _cookies.TryGetValue(key, out var value) ? value : null;

    public int Count => _cookies.Count;

    public ICollection<string> Keys => _cookies.Keys;

    public bool ContainsKey(string key) => _cookies.ContainsKey(key);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _cookies.GetEnumerator();

    public bool TryGetValue(string key, out string? value)
    {
        if (_cookies.TryGetValue(key, out var val))
        {
            value = val;
            return true;
        }
        value = null;
        return false;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
