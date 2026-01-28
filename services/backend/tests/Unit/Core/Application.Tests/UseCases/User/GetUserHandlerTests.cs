using Application.Interfaces;
using Application.UseCases.User.GetUser;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.User;

public class GetUserHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetUserHandler _handler;

    public GetUserHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetUserHandler(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsUserDto_WhenUserExists()
    {
        // Arrange
        var email = "test@example.com";
        var userId = Guid.NewGuid();
        var query = new GetUserQuery(email);

        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            Name = "Test User",
            AvatarUrl = "https://example.com/avatar.png",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        };

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal(email, result.Email);
        Assert.Equal("Test User", result.Name);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenUserNotFound()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var query = new GetUserQuery(email);

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync((UserEntity?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_CallsRepository_WithCorrectEmail()
    {
        // Arrange
        var email = "verify@example.com";
        var query = new GetUserQuery(email);

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync((UserEntity?)null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(x => x.GetByEmailAsync(email), Times.Once);
    }
}
