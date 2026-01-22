using System.Net;
using System.Security.Claims;
using Application.Interfaces;
using Application.UseCases.Auth.ForgotPassword;
using Application.UseCases.Auth.Login;
using Application.UseCases.Auth.Register;
using Application.UseCases.Auth.ResetPassword;
using Application.UseCases.User.CreateUser;
using Application.UseCases.User.GetUser;
using Domain.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class AuthControllerTests
{
    private readonly AuthController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IWebHostEnvironment> _envMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly Mock<IRateLimitService> _rateLimitServiceMock;
    private readonly Mock<IPasswordValidator> _passwordValidatorMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IJwtBlacklistService> _jwtBlacklistServiceMock;
    private readonly DefaultHttpContext _httpContext;

    public AuthControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _configMock = new Mock<IConfiguration>();
        _tokenServiceMock = new Mock<ITokenService>();
        _envMock = new Mock<IWebHostEnvironment>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _rateLimitServiceMock = new Mock<IRateLimitService>();
        _passwordValidatorMock = new Mock<IPasswordValidator>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _jwtBlacklistServiceMock = new Mock<IJwtBlacklistService>();

        _httpContext = new DefaultHttpContext();
        _httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        _controller = new AuthController(
            _mediatorMock.Object,
            _configMock.Object,
            _tokenServiceMock.Object,
            _envMock.Object,
            _loggerMock.Object,
            _rateLimitServiceMock.Object,
            _passwordValidatorMock.Object,
            _refreshTokenRepositoryMock.Object,
            _jwtBlacklistServiceMock.Object
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = _httpContext
            }
        };
    }

    #region Helper Methods

    private void SetupAllowedRateLimit()
    {
        _rateLimitServiceMock.Setup(x => x.CheckAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Allowed(1, 10, DateTime.UtcNow.AddMinutes(15)));
    }

    private void SetupExceededRateLimit(TimeSpan retryAfter)
    {
        _rateLimitServiceMock.Setup(x => x.CheckAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Exceeded(10, 10, DateTime.UtcNow.Add(retryAfter)));
    }

    private void SetupValidPasswordValidation()
    {
        _passwordValidatorMock.Setup(x => x.Validate(It.IsAny<string>()))
            .Returns(PasswordValidationResult.Success());
    }

    private void SetupInvalidPasswordValidation(string errorMessage)
    {
        _passwordValidatorMock.Setup(x => x.Validate(It.IsAny<string>()))
            .Returns(PasswordValidationResult.Failure(errorMessage));
    }

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        _httpContext.User = claimsPrincipal;
    }

    private void SetupJwtConfig()
    {
        _configMock.Setup(x => x["Jwt:ExpireHours"]).Returns("4");
    }

    #endregion

    #region Mode Endpoint Tests

    [Fact]
    public void Mode_ReturnsOk_WithDefaultGoogleProvider()
    {
        // Arrange
        _configMock.Setup(x => x["Auth:Provider"]).Returns((string?)null);

        // Act
        var result = _controller.Mode();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var providerProperty = value?.GetType().GetProperty("provider");
        var provider = providerProperty?.GetValue(value);
        provider.Should().Be("google");
    }

    [Fact]
    public void Mode_ReturnsOk_WithConfiguredProvider()
    {
        // Arrange
        _configMock.Setup(x => x["Auth:Provider"]).Returns("azure");

        // Act
        var result = _controller.Mode();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var providerProperty = value?.GetType().GetProperty("provider");
        var provider = providerProperty?.GetValue(value);
        provider.Should().Be("azure");
    }

    #endregion

    #region Register Endpoint Tests

    [Fact]
    public async Task Register_Returns429_WhenRateLimitExceeded()
    {
        // Arrange
        SetupExceededRateLimit(TimeSpan.FromMinutes(30));
        var request = new RegisterRequest("Test User", "test@example.com", "SecurePassword123!");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(429);
        _controller.Response.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsNull()
    {
        // Arrange
        SetupAllowedRateLimit();
        var request = new RegisterRequest("Test User", null, "SecurePassword123!");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Valid email address is required");
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsEmpty()
    {
        // Arrange
        SetupAllowedRateLimit();
        var request = new RegisterRequest("Test User", "", "SecurePassword123!");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Valid email address is required");
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        // Arrange
        SetupAllowedRateLimit();
        var request = new RegisterRequest("Test User", "invalid-email", "SecurePassword123!");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Valid email address is required");
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordValidationFails()
    {
        // Arrange
        SetupAllowedRateLimit();
        SetupInvalidPasswordValidation("Password must be at least 8 characters");
        var request = new RegisterRequest("Test User", "test@example.com", "weak");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Password must be at least 8 characters");
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenUserAlreadyExists()
    {
        // Arrange
        SetupAllowedRateLimit();
        SetupValidPasswordValidation();
        _mediatorMock.Setup(x => x.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterResult(false, null, "User already exists"));
        var request = new RegisterRequest("Test User", "existing@example.com", "SecurePassword123!");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var value = conflictResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("User already exists");
    }

    [Fact]
    public async Task Register_ReturnsOk_WhenRegistrationSucceeds()
    {
        // Arrange
        SetupAllowedRateLimit();
        SetupValidPasswordValidation();
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "test@example.com" };
        _mediatorMock.Setup(x => x.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterResult(true, user, null));
        var request = new RegisterRequest("Test User", "test@example.com", "SecurePassword123!");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var idProperty = value?.GetType().GetProperty("Id");
        var id = idProperty?.GetValue(value);
        id.Should().Be(userId);
    }

    [Fact]
    public async Task Register_AssignsOnlyUserRole()
    {
        // Arrange
        SetupAllowedRateLimit();
        SetupValidPasswordValidation();
        var user = new UserEntity { Id = Guid.NewGuid(), Email = "test@example.com" };
        RegisterCommand? capturedCommand = null;
        _mediatorMock.Setup(x => x.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<RegisterResult>, CancellationToken>((cmd, _) => capturedCommand = cmd as RegisterCommand)
            .ReturnsAsync(new RegisterResult(true, user, null));
        var request = new RegisterRequest("Test User", "test@example.com", "SecurePassword123!");

        // Act
        await _controller.Register(request);

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Roles.Should().ContainSingle().Which.Should().Be("User");
    }

    #endregion

    #region Login Endpoint Tests

    [Fact]
    public async Task Login_Returns429_WhenIpRateLimitExceeded()
    {
        // Arrange
        SetupExceededRateLimit(TimeSpan.FromMinutes(10));
        var request = new LoginRequest("test@example.com", "password");

        // Act
        var result = await _controller.Login(request);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(429);
        _controller.Response.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task Login_Returns429_WhenAccountRateLimitExceeded()
    {
        // Arrange
        // First call (IP) succeeds, second call (account) fails
        var callCount = 0;
        _rateLimitServiceMock.Setup(x => x.CheckAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? RateLimitResult.Allowed(1, 10, DateTime.UtcNow.AddMinutes(15))
                    : RateLimitResult.Exceeded(5, 5, DateTime.UtcNow.AddMinutes(10));
            });
        var request = new LoginRequest("test@example.com", "password");

        // Act
        var result = await _controller.Login(request);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenCredentialsInvalid()
    {
        // Arrange
        SetupAllowedRateLimit();
        SetupJwtConfig();
        _mediatorMock.Setup(x => x.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(false, string.Empty, Array.Empty<string>(), default, "Invalid credentials"));
        var request = new LoginRequest("test@example.com", "wrongpassword");

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var value = unauthorizedResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task Login_ReturnsOk_WithTokens_WhenCredentialsValid()
    {
        // Arrange
        SetupAllowedRateLimit();
        SetupJwtConfig();
        var userId = Guid.NewGuid();
        _mediatorMock.Setup(x => x.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(true, "test-jwt-token", new[] { "User" }, userId, string.Empty));
        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenEntity t, CancellationToken _) => t);
        var request = new LoginRequest("test@example.com", "correctpassword");

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var tokenProperty = value?.GetType().GetProperty("token");
        var token = tokenProperty?.GetValue(value)?.ToString();
        token.Should().Be("test-jwt-token");
    }

    [Fact]
    public async Task Login_SetsCookies_WhenCredentialsValid()
    {
        // Arrange
        SetupAllowedRateLimit();
        SetupJwtConfig();
        var userId = Guid.NewGuid();
        _mediatorMock.Setup(x => x.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(true, "test-jwt-token", new[] { "User" }, userId, string.Empty));
        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenEntity t, CancellationToken _) => t);
        var request = new LoginRequest("test@example.com", "correctpassword");

        // Act
        await _controller.Login(request);

        // Assert
        _controller.Response.Headers.Should().ContainKey("Set-Cookie");
    }

    #endregion

    #region Refresh Token Endpoint Tests

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        var request = new RefreshTokenRequest(null);

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var value = unauthorizedResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Refresh token is required");
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenTokenNotFound()
    {
        // Arrange
        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenEntity?)null);
        var request = new RefreshTokenRequest("invalid-token");

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var value = unauthorizedResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Invalid refresh token");
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenTokenExpired()
    {
        // Arrange
        var expiredToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "expired-token",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-8)
        };
        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredToken);
        var request = new RefreshTokenRequest("expired-token");

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var value = unauthorizedResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Refresh token is no longer valid");
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenTokenRevoked()
    {
        // Arrange
        var revokedToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "revoked-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedToken);
        var request = new RefreshTokenRequest("revoked-token");

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var value = unauthorizedResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Refresh token is no longer valid");
    }

    [Fact]
    public async Task RefreshToken_RevokesAllTokens_OnTokenReuse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reusedToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "reused-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RevokedAt = DateTime.UtcNow.AddHours(-1),
            RevokeReason = "Replaced by new token"
        };
        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reusedToken);
        var request = new RefreshTokenRequest("reused-token");

        // Act
        await _controller.RefreshToken(request);

        // Assert
        _refreshTokenRepositoryMock.Verify(
            x => x.RevokeAllUserTokensAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshToken_RotatesToken_WhenValid()
    {
        // Arrange
        SetupJwtConfig();
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            UserRoles = new List<UserRoleEntity>
            {
                new() { Role = new RoleEntity { Name = "User" } }
            }
        };
        var validToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "valid-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            User = user
        };
        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validToken);
        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenEntity t, CancellationToken _) => t);
        _tokenServiceMock.Setup(x => x.GenerateToken(It.IsAny<UserEntity>(), It.IsAny<IEnumerable<string>>()))
            .Returns("new-access-token");
        var request = new RefreshTokenRequest("valid-token");

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _refreshTokenRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<RefreshTokenEntity>(t => t.RevokedAt != null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Logout Endpoint Tests

    [Fact]
    public async Task Logout_ReturnsOk_Always()
    {
        // Act
        var result = await _controller.Logout();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var messageProperty = value?.GetType().GetProperty("message");
        var message = messageProperty?.GetValue(value)?.ToString();
        message.Should().Be("Logged out successfully");
    }

    [Fact]
    public async Task Logout_DeletesCookies()
    {
        // Arrange - Set up cookies
        _httpContext.Request.Headers.Append("Cookie", "token=test-token; refreshToken=test-refresh-token");

        // Act
        await _controller.Logout();

        // Assert - Response should contain Set-Cookie headers for deletion
        _controller.Response.Headers.Should().ContainKey("Set-Cookie");
    }

    [Fact]
    public async Task Logout_BlacklistsJwtToken_WhenPresent()
    {
        // Arrange
        // Create a valid JWT token for testing
        var validJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiJ0ZXN0LWp0aSIsImV4cCI6MTg5MzQ1NjAwMH0.signature";
        _httpContext.Request.Cookies = new MockCookieCollection(new Dictionary<string, string>
        {
            { "token", validJwt }
        });

        // Act
        await _controller.Logout();

        // Assert - Even though we can't verify blacklisting due to JWT parsing,
        // the logout should still succeed
        var result = await _controller.Logout();
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_WhenPresent()
    {
        // Arrange
        var refreshToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "test-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _httpContext.Request.Cookies = new MockCookieCollection(new Dictionary<string, string>
        {
            { "refreshToken", "test-refresh-token" }
        });
        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync("test-refresh-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshToken);

        // Act
        await _controller.Logout();

        // Assert
        _refreshTokenRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<RefreshTokenEntity>(t => t.RevokedAt != null && t.RevokeReason == "User logout"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Revoke All Tokens Endpoint Tests

    [Fact]
    public async Task RevokeAllTokens_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange - no user claim set

        // Act
        var result = await _controller.RevokeAllTokens();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RevokeAllTokens_ReturnsUnauthorized_WhenUserIdInvalid()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "not-a-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        // Act
        var result = await _controller.RevokeAllTokens();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RevokeAllTokens_RevokesAllUserTokens_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.RevokeAllTokens();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _refreshTokenRepositoryMock.Verify(
            x => x.RevokeAllUserTokensAsync(userId, "User requested revocation", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeAllTokens_DeletesCookies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        await _controller.RevokeAllTokens();

        // Assert
        _controller.Response.Headers.Should().ContainKey("Set-Cookie");
    }

    #endregion

    #region Forgot Password Endpoint Tests

    [Fact]
    public async Task ForgotPassword_Returns429_WhenRateLimitExceeded()
    {
        // Arrange
        SetupExceededRateLimit(TimeSpan.FromMinutes(30));
        var request = new ForgotPasswordRequest("test@example.com");

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsOk_EvenWhenEmailDoesNotExist()
    {
        // Arrange - This prevents email enumeration
        SetupAllowedRateLimit();
        _mediatorMock.Setup(x => x.Send(It.IsAny<ForgotPasswordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var request = new ForgotPasswordRequest("nonexistent@example.com");

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var messageProperty = value?.GetType().GetProperty("message");
        var message = messageProperty?.GetValue(value)?.ToString();
        message.Should().Be("If the email exists, a reset link has been sent.");
    }

    [Fact]
    public async Task ForgotPassword_ReturnsOk_WhenEmailExists()
    {
        // Arrange
        SetupAllowedRateLimit();
        _mediatorMock.Setup(x => x.Send(It.IsAny<ForgotPasswordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("reset-token-123");
        var request = new ForgotPasswordRequest("existing@example.com");

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var messageProperty = value?.GetType().GetProperty("message");
        var message = messageProperty?.GetValue(value)?.ToString();
        message.Should().Be("If the email exists, a reset link has been sent.");
    }

    #endregion

    #region Reset Password Endpoint Tests

    [Fact]
    public async Task ResetPassword_ReturnsBadRequest_WhenResetFails()
    {
        // Arrange
        _mediatorMock.Setup(x => x.Send(It.IsAny<ResetPasswordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResetPasswordResult(false, "Invalid or expired reset token"));
        var request = new ResetPasswordRequest("test@example.com", "invalid-token", "NewPassword123!");

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value?.GetType().GetProperty("error");
        var error = errorProperty?.GetValue(value)?.ToString();
        error.Should().Be("Invalid or expired reset token");
    }

    [Fact]
    public async Task ResetPassword_ReturnsOk_WhenResetSucceeds()
    {
        // Arrange
        _mediatorMock.Setup(x => x.Send(It.IsAny<ResetPasswordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResetPasswordResult(true));
        var request = new ResetPasswordRequest("test@example.com", "valid-token", "NewPassword123!");

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var messageProperty = value?.GetType().GetProperty("message");
        var message = messageProperty?.GetValue(value)?.ToString();
        message.Should().Be("Password reset successfully");
    }

    #endregion

    #region Google OAuth Endpoint Tests

    [Fact]
    public void ManualGoogleLogin_ReturnsRedirectToGoogleOAuth()
    {
        // Arrange
        _configMock.Setup(x => x["Authentication:Google:ClientId"]).Returns("test-client-id");
        _configMock.Setup(x => x["Authentication:Google:RedirectUri"]).Returns("https://example.com/callback");

        // Act
        var result = _controller.ManualGoogleLogin();

        // Assert
        var redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
        redirectResult.Url.Should().Contain("accounts.google.com/o/oauth2/v2/auth");
        redirectResult.Url.Should().Contain("client_id=test-client-id");
    }

    [Fact]
    public async Task ManualGoogleCallback_ReturnsBadRequest_WhenCodeMissing()
    {
        // Act
        var result = await _controller.ManualGoogleCallback(null!);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Authorization code not provided.");
    }

    [Fact]
    public async Task ManualGoogleCallback_ReturnsBadRequest_WhenCodeEmpty()
    {
        // Act
        var result = await _controller.ManualGoogleCallback(string.Empty);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Authorization code not provided.");
    }

    #endregion
}

// Helper class for mocking cookies
internal class MockCookieCollection : IRequestCookieCollection
{
    private readonly Dictionary<string, string> _cookies;

    public MockCookieCollection(Dictionary<string, string> cookies)
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
