using System.Security.Claims;
using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Services;

public class AuditServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<ILogger<AuditService>> _loggerMock;
    private readonly AuditService _service;

    public AuditServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _auditLogRepositoryMock = new Mock<IAuditLogRepository>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _loggerMock = new Mock<ILogger<AuditService>>();

        _unitOfWorkMock.Setup(u => u.AuditLogs).Returns(_auditLogRepositoryMock.Object);

        _service = new AuditService(
            _unitOfWorkMock.Object,
            _httpContextAccessorMock.Object,
            _loggerMock.Object);
    }

    #region LogAsync Tests

    [Fact]
    public async Task LogAsync_Should_CreateAuditLogEntry()
    {
        // Arrange
        var entry = new AuditLogEntry
        {
            UserId = Guid.NewGuid(),
            ActionType = AuditActionTypes.UserLogin,
            EntityType = "User",
            EntityId = "user-123",
            Success = true
        };

        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogAsync(entry);

        // Assert
        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        capturedEntity.Should().NotBeNull();
        capturedEntity!.ActionType.Should().Be(AuditActionTypes.UserLogin);
        capturedEntity.EntityType.Should().Be("User");
        capturedEntity.EntityId.Should().Be("user-123");
        capturedEntity.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LogAsync_Should_SerializeOldAndNewValues()
    {
        // Arrange
        var entry = new AuditLogEntry
        {
            ActionType = AuditActionTypes.UserUpdate,
            EntityType = "User",
            OldValues = new { Name = "Old Name" },
            NewValues = new { Name = "New Name" }
        };

        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogAsync(entry);

        // Assert
        capturedEntity!.OldValues.Should().Contain("Old Name");
        capturedEntity.NewValues.Should().Contain("New Name");
    }

    [Fact]
    public async Task LogAsync_Should_ExtractUserIdFromHttpContext_When_NotProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var httpContext = CreateAuthenticatedHttpContext(userId);
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        var entry = new AuditLogEntry
        {
            ActionType = AuditActionTypes.UserLogin,
            EntityType = "User"
        };

        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogAsync(entry);

        // Assert
        capturedEntity!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task LogAsync_Should_ExtractIpAddressFromXForwardedFor()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = "192.168.1.100, 10.0.0.1";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        var entry = new AuditLogEntry
        {
            ActionType = AuditActionTypes.UserLogin,
            EntityType = "User"
        };

        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogAsync(entry);

        // Assert
        capturedEntity!.IpAddress.Should().Be("192.168.1.100");
    }

    [Fact]
    public async Task LogAsync_Should_ExtractIpAddressFromXRealIp()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Real-IP"] = "10.0.0.50";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        var entry = new AuditLogEntry
        {
            ActionType = AuditActionTypes.UserLogin,
            EntityType = "User"
        };

        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogAsync(entry);

        // Assert
        capturedEntity!.IpAddress.Should().Be("10.0.0.50");
    }

    [Fact]
    public async Task LogAsync_Should_ExtractUserAgent()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 Test Browser";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        var entry = new AuditLogEntry
        {
            ActionType = AuditActionTypes.UserLogin,
            EntityType = "User"
        };

        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogAsync(entry);

        // Assert
        capturedEntity!.UserAgent.Should().Be("Mozilla/5.0 Test Browser");
    }

    [Fact]
    public async Task LogAsync_Should_NotThrow_On_Exception()
    {
        // Arrange
        var entry = new AuditLogEntry
        {
            ActionType = AuditActionTypes.UserLogin,
            EntityType = "User"
        };

        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var act = () => _service.LogAsync(entry);

        // Assert - Should not throw, just log the error
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogAsync_Should_SetTimestamp()
    {
        // Arrange
        var beforeCall = DateTime.UtcNow;
        var entry = new AuditLogEntry
        {
            ActionType = AuditActionTypes.UserLogin,
            EntityType = "User"
        };

        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogAsync(entry);

        // Assert
        capturedEntity!.Timestamp.Should().BeOnOrAfter(beforeCall);
        capturedEntity.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region LogSuccessAsync Tests

    [Fact]
    public async Task LogSuccessAsync_Should_CreateSuccessEntry()
    {
        // Arrange
        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogSuccessAsync(AuditActionTypes.UserLogin, "User", "user-123");

        // Assert
        capturedEntity!.ActionType.Should().Be(AuditActionTypes.UserLogin);
        capturedEntity.EntityType.Should().Be("User");
        capturedEntity.EntityId.Should().Be("user-123");
        capturedEntity.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LogSuccessAsync_Should_IncludeOldAndNewValues()
    {
        // Arrange
        var oldValues = new { Credits = 100 };
        var newValues = new { Credits = 50 };
        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogSuccessAsync(AuditActionTypes.CreditConsume, "Subscription", "sub-123", oldValues, newValues);

        // Assert
        capturedEntity!.OldValues.Should().Contain("100");
        capturedEntity.NewValues.Should().Contain("50");
    }

    [Fact]
    public async Task LogSuccessAsync_Should_IncludeMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { { "source", "api" }, { "version", "1.0" } };
        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogSuccessAsync(AuditActionTypes.UserLogin, "User", metadata: metadata);

        // Assert
        capturedEntity!.Metadata.Should().Contain("api");
        capturedEntity.Metadata.Should().Contain("1.0");
    }

    #endregion

    #region LogFailureAsync Tests

    [Fact]
    public async Task LogFailureAsync_Should_CreateFailureEntry()
    {
        // Arrange
        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogFailureAsync(AuditActionTypes.UserLoginFailed, "User", "user-123", "Invalid credentials");

        // Assert
        capturedEntity!.ActionType.Should().Be(AuditActionTypes.UserLoginFailed);
        capturedEntity.Success.Should().BeFalse();
        capturedEntity.ErrorMessage.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task LogFailureAsync_Should_SetWarningSeverity()
    {
        // Arrange
        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogFailureAsync(AuditActionTypes.UserLoginFailed, "User", "user-123", "Error");

        // Assert
        capturedEntity!.Severity.Should().Be(AuditSeverity.Warning);
    }

    #endregion

    #region QueryAsync Tests

    [Fact]
    public async Task QueryAsync_Should_DelegateToRepository()
    {
        // Arrange
        var query = new AuditLogQuery { UserId = Guid.NewGuid() };
        var expectedLogs = new List<AuditLogEntity>
        {
            new() { ActionType = AuditActionTypes.UserLogin },
            new() { ActionType = AuditActionTypes.UserLogout }
        };

        _auditLogRepositoryMock
            .Setup(x => x.QueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLogs);

        // Act
        var result = await _service.QueryAsync(query);

        // Assert
        result.Should().HaveCount(2);
        _auditLogRepositoryMock.Verify(x => x.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Severity Tests

    [Theory]
    [InlineData(AuditActionTypes.UserDelete, AuditSeverity.Critical)]
    [InlineData(AuditActionTypes.PasswordReset, AuditSeverity.Critical)]
    [InlineData(AuditActionTypes.CreditLimitExceeded, AuditSeverity.Critical)]
    [InlineData(AuditActionTypes.ChatAccessDenied, AuditSeverity.Critical)]
    public async Task LogSuccessAsync_Should_SetCriticalSeverity_ForCriticalActions(string actionType, AuditSeverity expectedSeverity)
    {
        // Arrange
        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogSuccessAsync(actionType, "Entity");

        // Assert
        capturedEntity!.Severity.Should().Be(expectedSeverity);
    }

    [Theory]
    [InlineData(AuditActionTypes.UserLogin, AuditSeverity.Info)]
    [InlineData(AuditActionTypes.CreditConsume, AuditSeverity.Info)]
    [InlineData(AuditActionTypes.ChatMessage, AuditSeverity.Info)]
    public async Task LogSuccessAsync_Should_SetInfoSeverity_ForRegularActions(string actionType, AuditSeverity expectedSeverity)
    {
        // Arrange
        AuditLogEntity? capturedEntity = null;
        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((AuditLogEntity e, CancellationToken _) => e);

        // Act
        await _service.LogSuccessAsync(actionType, "Entity");

        // Assert
        capturedEntity!.Severity.Should().Be(expectedSeverity);
    }

    #endregion

    #region Helper Methods

    private static DefaultHttpContext CreateAuthenticatedHttpContext(Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        httpContext.User = new ClaimsPrincipal(identity);
        return httpContext;
    }

    #endregion
}
