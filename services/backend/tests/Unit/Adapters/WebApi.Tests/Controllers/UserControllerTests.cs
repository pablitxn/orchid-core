using System.Security.Claims;
using Application.UseCases.User.GetUser;
using Domain.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class UserControllerTests
{
    private readonly UserController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly DefaultHttpContext _httpContext;

    public UserControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _httpContext = new DefaultHttpContext();

        _controller = new UserController(_mediatorMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = _httpContext
            }
        };
    }

    #region Helper Methods

    private void SetupAuthenticatedUser(string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        _httpContext.User = claimsPrincipal;
    }

    private void SetupUnauthenticatedUser()
    {
        _httpContext.User = new ClaimsPrincipal();
    }

    #endregion

    #region GetUser Endpoint Tests

    [Fact]
    public async Task GetUser_ReturnsUnauthorized_WhenEmailClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetUser();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUser_ReturnsUnauthorized_WhenEmailClaimEmpty()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, "")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        // Act
        var result = await _controller.GetUser();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUser_ReturnsUnauthorized_WhenEmailClaimWhitespace()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, "   ")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        // Act
        var result = await _controller.GetUser();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUser_ReturnsOk_WhenUserNotFound()
    {
        // Arrange
        SetupAuthenticatedUser("nonexistent@example.com");
        _mediatorMock.Setup(x => x.Send(It.IsAny<GetUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity)null!);

        // Act
        var result = await _controller.GetUser();

        // Assert
        // Based on the controller code, it returns Ok() when user is null (with a TODO comment)
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task GetUser_ReturnsOk_WithUserData_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "test@example.com";
        var name = "Test User";

        SetupAuthenticatedUser(email);
        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            Name = name
        };
        _mediatorMock.Setup(x => x.Send(It.IsAny<GetUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.GetUser();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;

        var idProperty = value?.GetType().GetProperty("Id");
        var emailProperty = value?.GetType().GetProperty("Email");
        var nameProperty = value?.GetType().GetProperty("Name");

        idProperty?.GetValue(value).Should().Be(userId);
        emailProperty?.GetValue(value)?.ToString().Should().Be(email);
        nameProperty?.GetValue(value)?.ToString().Should().Be(name);
    }

    [Fact]
    public async Task GetUser_PassesCorrectEmail_ToMediator()
    {
        // Arrange
        var email = "specific@example.com";
        SetupAuthenticatedUser(email);
        _mediatorMock.Setup(x => x.Send(It.IsAny<GetUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEntity { Id = Guid.NewGuid(), Email = email, Name = "Test" });

        // Act
        await _controller.GetUser();

        // Assert
        _mediatorMock.Verify(
            x => x.Send(It.Is<GetUserCommand>(cmd => cmd.Email == email), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUser_ReturnsOk_WithNullName_WhenNameNotSet()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "test@example.com";

        SetupAuthenticatedUser(email);
        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            Name = null
        };
        _mediatorMock.Setup(x => x.Send(It.IsAny<GetUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.GetUser();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var nameProperty = value?.GetType().GetProperty("Name");
        nameProperty?.GetValue(value).Should().BeNull();
    }

    #endregion
}
