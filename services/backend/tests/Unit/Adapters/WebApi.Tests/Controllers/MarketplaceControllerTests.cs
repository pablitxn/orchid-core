using System.Security.Claims;
using Application.UseCases.Marketplace.GetUserMarketplaceItems;
using Application.UseCases.Plugin.PurchasePlugin;
using Application.UseCases.Workflow.PurchaseWorkflow;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class MarketplaceControllerTests
{
    private readonly MarketplaceController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public MarketplaceControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new MarketplaceController(_mediatorMock.Object);
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

    #region GetUserItems Tests

    [Fact]
    public async Task GetUserItems_ReturnsOk_WhenUserIsAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedItems = new UserMarketplaceItemsDto
        {
            Plugins = new List<UserPluginDto>
            {
                new() { Id = Guid.NewGuid(), Name = "Plugin 1", Description = "Desc 1" }
            },
            Workflows = new List<UserWorkflowDto>
            {
                new() { Id = Guid.NewGuid(), Name = "Workflow 1", Description = "Desc 1" }
            }
        };
        
        _mediatorMock.Setup(m => m.Send(
            It.Is<GetUserMarketplaceItemsQuery>(q => q.UserId == userId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItems);

        // Act
        var result = await _controller.GetUserItems();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedItems, okResult.Value);
    }

    [Fact]
    public async Task GetUserItems_ReturnsUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetUserItems();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetUserItems_ReturnsUnauthorized_WhenMediatorThrowsUnauthorizedException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<GetUserMarketplaceItemsQuery>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var result = await _controller.GetUserItems();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    #endregion

    #region GetMyItems Tests

    [Fact]
    public async Task GetMyItems_ReturnsOk_WhenUserIsAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedItems = new UserMarketplaceItemsDto
        {
            Plugins = new List<UserPluginDto>
            {
                new() { Id = Guid.NewGuid(), Name = "Plugin 1", Description = "Desc 1" }
            }
        };
        
        _mediatorMock.Setup(m => m.Send(
            It.Is<GetUserMarketplaceItemsQuery>(q => q.UserId == userId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItems);

        // Act
        var result = await _controller.GetMyItems();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedItems, okResult.Value);
    }

    #endregion

    #region PurchasePlugin Tests

    [Fact]
    public async Task PurchasePlugin_ReturnsOk_WhenPurchaseIsSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var successResult = new PurchasePluginDto(true);
        _mediatorMock.Setup(m => m.Send(
            It.Is<PurchasePluginCommand>(c => c.UserId == userId && c.PluginId == pluginId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        // Act
        var result = await _controller.PurchasePlugin(pluginId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic response = okResult.Value!;
        var successProp = okResult.Value!.GetType().GetProperty("success");
        var messageProp = okResult.Value!.GetType().GetProperty("message");
        
        Assert.NotNull(successProp);
        Assert.NotNull(messageProp);
        Assert.True((bool)successProp.GetValue(okResult.Value)!);
        Assert.Equal("Plugin purchased successfully", messageProp.GetValue(okResult.Value));
    }

    [Fact]
    public async Task PurchasePlugin_ReturnsBadRequest_WhenPurchaseFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var errorMessage = "Insufficient credits";
        var failureResult = new PurchasePluginDto(false, errorMessage);
        _mediatorMock.Setup(m => m.Send(
            It.Is<PurchasePluginCommand>(c => c.UserId == userId && c.PluginId == pluginId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResult);

        // Act
        var result = await _controller.PurchasePlugin(pluginId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(errorMessage, badRequestResult.Value);
    }

    [Fact]
    public async Task PurchasePlugin_ReturnsUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.PurchasePlugin(pluginId, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task PurchasePlugin_ReturnsUnauthorized_WhenMediatorThrowsUnauthorizedException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<PurchasePluginCommand>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var result = await _controller.PurchasePlugin(pluginId, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task PurchasePlugin_ReturnsBadRequest_WhenExceptionIsThrown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var exceptionMessage = "Database error";
        _mediatorMock.Setup(m => m.Send(
            It.IsAny<PurchasePluginCommand>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        var result = await _controller.PurchasePlugin(pluginId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(exceptionMessage, badRequestResult.Value);
    }

    #endregion

    #region PurchaseWorkflow Tests

    [Fact]
    public async Task PurchaseWorkflow_ReturnsOk_WhenPurchaseIsSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.Is<PurchaseWorkflowCommand>(c => c.UserId == userId && c.WorkflowId == workflowId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurchaseOutcome.Success);

        // Act
        var result = await _controller.PurchaseWorkflow(workflowId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var successProp = okResult.Value!.GetType().GetProperty("success");
        var messageProp = okResult.Value!.GetType().GetProperty("message");
        
        Assert.NotNull(successProp);
        Assert.NotNull(messageProp);
        Assert.True((bool)successProp.GetValue(okResult.Value)!);
        Assert.Equal("Workflow purchased successfully", messageProp.GetValue(okResult.Value));
    }

    [Fact]
    public async Task PurchaseWorkflow_ReturnsBadRequest_WhenWorkflowNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<PurchaseWorkflowCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurchaseOutcome.WorkflowNotFound);

        // Act
        var result = await _controller.PurchaseWorkflow(workflowId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Workflow not found", badRequestResult.Value);
    }

    [Fact]
    public async Task PurchaseWorkflow_ReturnsBadRequest_WhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<PurchaseWorkflowCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurchaseOutcome.UserNotFound);

        // Act
        var result = await _controller.PurchaseWorkflow(workflowId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("User not found", badRequestResult.Value);
    }

    [Fact]
    public async Task PurchaseWorkflow_ReturnsBadRequest_WhenInsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<PurchaseWorkflowCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurchaseOutcome.InsufficientCredits);

        // Act
        var result = await _controller.PurchaseWorkflow(workflowId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Insufficient credits", badRequestResult.Value);
    }

    [Fact]
    public async Task PurchaseWorkflow_ReturnsBadRequest_WhenAlreadyPurchased()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<PurchaseWorkflowCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurchaseOutcome.AlreadyPurchased);

        // Act
        var result = await _controller.PurchaseWorkflow(workflowId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("You have already purchased this workflow", badRequestResult.Value);
    }

    [Fact]
    public async Task PurchaseWorkflow_ReturnsUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.PurchaseWorkflow(workflowId, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task PurchaseWorkflow_ReturnsUnauthorized_WhenMediatorThrowsUnauthorizedException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<PurchaseWorkflowCommand>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var result = await _controller.PurchaseWorkflow(workflowId, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task PurchaseWorkflow_ReturnsBadRequest_WhenExceptionIsThrown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var exceptionMessage = "Database error";
        _mediatorMock.Setup(m => m.Send(
            It.IsAny<PurchaseWorkflowCommand>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        var result = await _controller.PurchaseWorkflow(workflowId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(exceptionMessage, badRequestResult.Value);
    }

    #endregion
}
