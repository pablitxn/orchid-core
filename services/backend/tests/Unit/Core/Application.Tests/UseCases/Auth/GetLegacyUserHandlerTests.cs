using Application.Interfaces;
using Application.UseCases.Auth.GetLegacyUser;
using Domain.Entities;
using Domain.Enums;
using Moq;

namespace Application.Tests.UseCases.Auth;

public class GetLegacyUserHandlerTests
{
    private readonly GetLegacyUserHandler _handler;
    private readonly Mock<IUserRepository> _userRepo = new();

    public GetLegacyUserHandlerTests()
    {
        _handler = new GetLegacyUserHandler(_userRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsUser_WhenUserExistsWithPassword()
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

        var query = new GetLegacyUserQuery("test@example.com");

        _userRepo.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.Name, result.Name);
        Assert.Equal(user.AvatarUrl, result.AvatarUrl);
        Assert.False(result.IsMigrated);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenUserNotFound()
    {
        // Arrange
        var query = new GetLegacyUserQuery("notfound@example.com");

        _userRepo.Setup(r => r.GetByEmailAsync("notfound@example.com"))
            .ReturnsAsync((UserEntity?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenUserHasNoPassword()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "oauth@example.com",
            PasswordHash = "" // OAuth-only user
        };

        var query = new GetLegacyUserQuery("oauth@example.com");

        _userRepo.Setup(r => r.GetByEmailAsync("oauth@example.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenUserHasNullPassword()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "oauth@example.com",
            PasswordHash = null! // OAuth-only user (null instead of empty)
        };

        var query = new GetLegacyUserQuery("oauth@example.com");

        _userRepo.Setup(r => r.GetByEmailAsync("oauth@example.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsMigratedTrue_WhenUserAlreadyMigrated()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "migrated@example.com",
            Name = "Migrated User",
            PasswordHash = "hashedpassword",
            Auth0MigrationStatus = Auth0MigrationStatus.Completed
        };

        var query = new GetLegacyUserQuery("migrated@example.com");

        _userRepo.Setup(r => r.GetByEmailAsync("migrated@example.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsMigrated);
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

        var query = new GetLegacyUserQuery("TEST@EXAMPLE.COM");

        _userRepo.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        _userRepo.Verify(r => r.GetByEmailAsync("test@example.com"), Times.Once);
        Assert.NotNull(result);
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

        var query = new GetLegacyUserQuery("  test@example.com  ");

        _userRepo.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        _userRepo.Verify(r => r.GetByEmailAsync("test@example.com"), Times.Once);
        Assert.NotNull(result);
    }
}
