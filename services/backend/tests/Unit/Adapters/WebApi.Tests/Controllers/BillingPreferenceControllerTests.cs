using System.Security.Claims;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class BillingPreferenceControllerTests
{
    private readonly BillingPreferenceController _controller;
    private readonly Mock<IUserBillingPreferenceRepository> _repositoryMock;
    private readonly Mock<ILogger<BillingPreferenceController>> _loggerMock;

    public BillingPreferenceControllerTests()
    {
        _repositoryMock = new Mock<IUserBillingPreferenceRepository>();
        _loggerMock = new Mock<ILogger<BillingPreferenceController>>();
        _controller = new BillingPreferenceController(_repositoryMock.Object, _loggerMock.Object);
    }

    #region Helper Methods

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private void SetupUnauthenticatedUser()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };
    }

    #endregion

    #region GetBillingPreferences Tests

    [Fact]
    public async Task GetBillingPreferences_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetBillingPreferences();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetBillingPreferences_ReturnsOk_WithDefaultPreferences_WhenNoPreferencesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBillingPreferenceEntity?)null);

        // Act
        var result = await _controller.GetBillingPreferences();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var preferences = okResult.Value;
        Assert.NotNull(preferences);
        
        var messageBillingMethodProperty = preferences!.GetType().GetProperty("messageBillingMethod");
        Assert.NotNull(messageBillingMethodProperty);
        Assert.Equal("tokens", messageBillingMethodProperty!.GetValue(preferences));
    }

    [Fact]
    public async Task GetBillingPreferences_ReturnsOk_WithExistingPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var preferences = new UserBillingPreferenceEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MessageBillingMethod = "fixed",
            TokenRate = 0.002m,
            FixedMessageRate = 10,
            LowCreditThreshold = 200,
            EnableLowCreditAlerts = false,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        // Act
        var result = await _controller.GetBillingPreferences();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetBillingPreferences_CallsRepository_WithCorrectUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBillingPreferenceEntity?)null);

        // Act
        await _controller.GetBillingPreferences();

        // Assert
        _repositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBillingPreferences_ReturnsInternalServerError_WhenExceptionOccurs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetBillingPreferences();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    #endregion

    #region UpdateBillingPreferences Tests

    [Fact]
    public async Task UpdateBillingPreferences_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var request = new UpdateBillingPreferencesRequest
        {
            MessageBillingMethod = "tokens",
            TokenRate = 0.001m,
            FixedMessageRate = 5
        };

        // Act
        var result = await _controller.UpdateBillingPreferences(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task UpdateBillingPreferences_ReturnsBadRequest_WhenInvalidBillingMethod()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var request = new UpdateBillingPreferencesRequest
        {
            MessageBillingMethod = "invalid",
            TokenRate = 0.001m,
            FixedMessageRate = 5
        };

        // Act
        var result = await _controller.UpdateBillingPreferences(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = badRequestResult.Value;
        Assert.NotNull(error);
        
        var errorProperty = error!.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        var errorMessage = errorProperty!.GetValue(error) as string;
        Assert.Contains("Invalid billing method", errorMessage);
    }

    [Fact]
    public async Task UpdateBillingPreferences_ReturnsBadRequest_WhenNegativeTokenRate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var request = new UpdateBillingPreferencesRequest
        {
            MessageBillingMethod = "tokens",
            TokenRate = -0.001m,
            FixedMessageRate = 5
        };

        // Act
        var result = await _controller.UpdateBillingPreferences(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = badRequestResult.Value;
        Assert.NotNull(error);
        
        var errorProperty = error!.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        var errorMessage = errorProperty!.GetValue(error) as string;
        Assert.Contains("Rates must be positive", errorMessage);
    }

    [Fact]
    public async Task UpdateBillingPreferences_ReturnsBadRequest_WhenNegativeFixedMessageRate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var request = new UpdateBillingPreferencesRequest
        {
            MessageBillingMethod = "tokens",
            TokenRate = 0.001m,
            FixedMessageRate = -5
        };

        // Act
        var result = await _controller.UpdateBillingPreferences(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = badRequestResult.Value;
        Assert.NotNull(error);
        
        var errorProperty = error!.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        var errorMessage = errorProperty!.GetValue(error) as string;
        Assert.Contains("Rates must be positive", errorMessage);
    }

    [Fact]
    public async Task UpdateBillingPreferences_CreatesNewPreferences_WhenNoneExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var request = new UpdateBillingPreferencesRequest
        {
            MessageBillingMethod = "tokens",
            TokenRate = 0.002m,
            FixedMessageRate = 10,
            LowCreditThreshold = 150,
            EnableLowCreditAlerts = false
        };

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBillingPreferenceEntity?)null);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<UserBillingPreferenceEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBillingPreferenceEntity p, CancellationToken ct) => p);

        // Act
        var result = await _controller.UpdateBillingPreferences(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        _repositoryMock.Verify(
            r => r.CreateAsync(It.Is<UserBillingPreferenceEntity>(p =>
                p.UserId == userId &&
                p.MessageBillingMethod == request.MessageBillingMethod &&
                p.TokenRate == request.TokenRate &&
                p.FixedMessageRate == request.FixedMessageRate)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateBillingPreferences_UpdatesExistingPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var existingPreferences = new UserBillingPreferenceEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MessageBillingMethod = "tokens",
            TokenRate = 0.001m,
            FixedMessageRate = 5,
            LowCreditThreshold = 100,
            EnableLowCreditAlerts = true,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var request = new UpdateBillingPreferencesRequest
        {
            MessageBillingMethod = "fixed",
            TokenRate = 0.003m,
            FixedMessageRate = 15,
            LowCreditThreshold = 200,
            EnableLowCreditAlerts = false
        };

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPreferences);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<UserBillingPreferenceEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBillingPreferenceEntity p, CancellationToken ct) => p);

        // Act
        var result = await _controller.UpdateBillingPreferences(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.Is<UserBillingPreferenceEntity>(p =>
                p.MessageBillingMethod == request.MessageBillingMethod &&
                p.TokenRate == request.TokenRate &&
                p.FixedMessageRate == request.FixedMessageRate &&
                p.LowCreditThreshold == request.LowCreditThreshold &&
                p.EnableLowCreditAlerts == request.EnableLowCreditAlerts)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateBillingPreferences_AcceptsTokensBillingMethod()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var request = new UpdateBillingPreferencesRequest
        {
            MessageBillingMethod = "tokens",
            TokenRate = 0.001m,
            FixedMessageRate = 5
        };

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBillingPreferenceEntity?)null);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<UserBillingPreferenceEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBillingPreferenceEntity p, CancellationToken ct) => p);

        // Act
        var result = await _controller.UpdateBillingPreferences(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateBillingPreferences_AcceptsFixedBillingMethod()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var request = new UpdateBillingPreferencesRequest
        {
            MessageBillingMethod = "fixed",
            TokenRate = 0.001m,
            FixedMessageRate = 5
        };

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBillingPreferenceEntity?)null);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<UserBillingPreferenceEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserBillingPreferenceEntity p, CancellationToken ct) => p);

        // Act
        var result = await _controller.UpdateBillingPreferences(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateBillingPreferences_ReturnsInternalServerError_WhenExceptionOccurs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var request = new UpdateBillingPreferencesRequest
        {
            MessageBillingMethod = "tokens",
            TokenRate = 0.001m,
            FixedMessageRate = 5
        };

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.UpdateBillingPreferences(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task BillingPreferenceController_HasAuthorizeAttribute()
    {
        // Arrange
        var controllerType = typeof(BillingPreferenceController);

        // Assert
        var authorizeAttribute = controllerType.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        Assert.Single(authorizeAttribute);
    }

    #endregion
}
