using Application.Interfaces;
using Application.UseCases.Auth.ValidateLegacyCredentials;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.Auth;

public class ValidateLegacyCredentialsHandlerTests
{
    private readonly ValidateLegacyCredentialsHandler _handler;
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ILogger<ValidateLegacyCredentialsHandler>> _logger = new();

    public ValidateLegacyCredentialsHandlerTests()
    {
        _handler = new ValidateLegacyCredentialsHandler(
            _userRepo.Object,
            _passwordHasher.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenCredentialsValid()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Name = "Test User",
            AvatarUrl = "https://example.com/avatar.png",
            PasswordHash = "hashedpassword",
            Auth0MigrationStatus = Auth0MigrationStatus.NotMigrated
        };

        var command = new ValidateLegacyCredentialsCommand("test@example.com", "password123");

        _userRepo.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword(user.PasswordHash, "password123"))
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.Name, result.Name);
        Assert.Equal(user.AvatarUrl, result.AvatarUrl);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenUserNotFound()
    {
        // Arrange
        var command = new ValidateLegacyCredentialsCommand("notfound@example.com", "password123");

        _userRepo.Setup(r => r.GetByEmailAsync("notfound@example.com"))
            .ReturnsAsync((UserEntity?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(result.UserId);
        Assert.Equal("User not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenUserAlreadyMigrated()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "migrated@example.com",
            PasswordHash = "hashedpassword",
            Auth0MigrationStatus = Auth0MigrationStatus.Completed
        };

        var command = new ValidateLegacyCredentialsCommand("migrated@example.com", "password123");

        _userRepo.Setup(r => r.GetByEmailAsync("migrated@example.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("User already migrated to Auth0", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenUserHasNoPassword()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "oauth@example.com",
            PasswordHash = "", // OAuth-only user
            Auth0MigrationStatus = Auth0MigrationStatus.NotMigrated
        };

        var command = new ValidateLegacyCredentialsCommand("oauth@example.com", "password123");

        _userRepo.Setup(r => r.GetByEmailAsync("oauth@example.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("User has no legacy password", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenPasswordInvalid()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashedpassword",
            Auth0MigrationStatus = Auth0MigrationStatus.NotMigrated
        };

        var command = new ValidateLegacyCredentialsCommand("test@example.com", "wrongpassword");

        _userRepo.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword(user.PasswordHash, "wrongpassword"))
            .Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid credentials", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_NormalizesEmail_ToLowerCase()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashedpassword",
            Auth0MigrationStatus = Auth0MigrationStatus.NotMigrated
        };

        var command = new ValidateLegacyCredentialsCommand("TEST@EXAMPLE.COM", "password123");

        _userRepo.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword(user.PasswordHash, "password123"))
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepo.Verify(r => r.GetByEmailAsync("test@example.com"), Times.Once);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Handle_TrimsEmail_Whitespace()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashedpassword",
            Auth0MigrationStatus = Auth0MigrationStatus.NotMigrated
        };

        var command = new ValidateLegacyCredentialsCommand("  test@example.com  ", "password123");

        _userRepo.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword(user.PasswordHash, "password123"))
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepo.Verify(r => r.GetByEmailAsync("test@example.com"), Times.Once);
        Assert.True(result.IsValid);
    }
}
