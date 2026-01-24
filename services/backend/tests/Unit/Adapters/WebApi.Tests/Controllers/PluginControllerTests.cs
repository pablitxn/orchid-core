using System.Security.Claims;
using Application.UseCases.Agent.AddPluginToAgent;
using Application.UseCases.Agent.RemovePluginFromAgent;
using Application.UseCases.Plugin.Common;
using Application.UseCases.Plugin.DeletePlugin;
using Application.UseCases.Plugin.ListAvailablePlugins;
using Application.UseCases.Plugin.ListPlugins;
using Application.UseCases.Plugin.PurchasePlugin;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class PluginControllerTests
{
    private readonly PluginController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public PluginControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new PluginController(_mediatorMock.Object);
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

    #region ListPlugins Tests

    [Fact]
    public async Task ListPlugins_ReturnsOk_WithPluginList()
    {
        // Arrange
        var expectedPlugins = new List<PluginDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Plugin 1" },
            new() { Id = Guid.NewGuid(), Name = "Plugin 2" }
        };

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlugins);

        // Act
        var result = await _controller.ListPlugins(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedPlugins, okResult.Value);
    }

    [Fact]
    public async Task ListPlugins_ReturnsOk_WithEmptyList_WhenNoPluginsAvailable()
    {
        // Arrange
        var emptyList = new List<PluginDto>();

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyList);

        // Act
        var result = await _controller.ListPlugins(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<PluginDto>>(okResult.Value);
        Assert.Empty(list);
    }

    #endregion

    #region ListAvailablePlugins Tests

    [Fact]
    public async Task ListAvailablePlugins_ReturnsOk_WithAvailablePluginDtos()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedPlugins = new List<AvailablePluginDto>
        {
            new (Guid.NewGuid(), "Available Plugin 1", "Desc 1", 100, false, false, true),
            new (Guid.NewGuid(), "Available Plugin 2", "Desc 2", 200, true, true, false)
        };

        var queryResult = new ListAvailablePluginsDto(expectedPlugins);

        _mediatorMock.Setup(m => m.Send(
            It.Is<ListAvailablePluginsQuery>(q => q.UserId == userId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _controller.ListAvailablePlugins(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        var availablePluginsProp = response!.GetType().GetProperty("availablePlugins");
        Assert.NotNull(availablePluginsProp);
        Assert.Equal(expectedPlugins, availablePluginsProp.GetValue(response));
    }

    [Fact]
    public async Task ListAvailablePlugins_ReturnsOk_WithEmptyList_WhenNoPluginsAvailable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var queryResult = new ListAvailablePluginsDto(new List<AvailablePluginDto>());

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListAvailablePluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _controller.ListAvailablePlugins(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        var availablePluginsProp = response!.GetType().GetProperty("availablePlugins");
        Assert.NotNull(availablePluginsProp);
        var plugins = availablePluginsProp.GetValue(response) as IEnumerable<object>;
        Assert.Empty(plugins!);
    }

    [Fact]
    public async Task ListAvailablePlugins_ThrowsUnauthorizedException_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.ListAvailablePlugins(CancellationToken.None));
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
        var response = okResult.Value;
        var messageProp = response!.GetType().GetProperty("message");
        Assert.NotNull(messageProp);
        Assert.Equal("Plugin purchased successfully", messageProp.GetValue(response));
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
        var response = badRequestResult.Value;
        var errorProp = response!.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Equal(errorMessage, errorProp.GetValue(response));
    }

    [Fact]
    public async Task PurchasePlugin_ThrowsUnauthorizedException_WhenUserNotAuthenticated()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        SetupUnauthenticatedUser();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.PurchasePlugin(pluginId, CancellationToken.None));
    }

    #endregion

    #region AddPluginToAgent Tests

    [Fact]
    public async Task AddPluginToAgent_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var successResult = new AddPluginToAgentDto(true);
        _mediatorMock.Setup(m => m.Send(
            It.Is<AddPluginToAgentCommand>(c =>
                c.UserId == userId && c.AgentId == agentId && c.PluginId == pluginId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        // Act
        var result = await _controller.AddPluginToAgent(agentId, pluginId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        var messageProp = response!.GetType().GetProperty("message");
        Assert.NotNull(messageProp);
        Assert.Equal("Plugin added to agent successfully", messageProp.GetValue(response));
    }

    [Fact]
    public async Task AddPluginToAgent_ReturnsBadRequest_WhenOperationFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var errorMessage = "Plugin already added to agent";
        var failureResult = new AddPluginToAgentDto(false, errorMessage);
        _mediatorMock.Setup(m => m.Send(
            It.IsAny<AddPluginToAgentCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResult);

        // Act
        var result = await _controller.AddPluginToAgent(agentId, pluginId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        var errorProp = response!.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Equal(errorMessage, errorProp.GetValue(response));
    }

    [Fact]
    public async Task AddPluginToAgent_ThrowsUnauthorizedException_WhenUserNotAuthenticated()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupUnauthenticatedUser();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.AddPluginToAgent(agentId, pluginId, CancellationToken.None));
    }

    #endregion

    #region RemovePluginFromAgent Tests

    [Fact]
    public async Task RemovePluginFromAgent_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var successResult = new RemovePluginFromAgentDto(true);
        _mediatorMock.Setup(m => m.Send(
            It.Is<RemovePluginFromAgentCommand>(c =>
                c.UserId == userId && c.AgentId == agentId && c.PluginId == pluginId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        // Act
        var result = await _controller.RemovePluginFromAgent(agentId, pluginId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        var messageProp = response!.GetType().GetProperty("message");
        Assert.NotNull(messageProp);
        Assert.Equal("Plugin removed from agent successfully", messageProp.GetValue(response));
    }

    [Fact]
    public async Task RemovePluginFromAgent_ReturnsBadRequest_WhenOperationFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var errorMessage = "Plugin not found on agent";
        var failureResult = new RemovePluginFromAgentDto(false, errorMessage);
        _mediatorMock.Setup(m => m.Send(
            It.IsAny<RemovePluginFromAgentCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResult);

        // Act
        var result = await _controller.RemovePluginFromAgent(agentId, pluginId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        var errorProp = response!.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Equal(errorMessage, errorProp.GetValue(response));
    }

    [Fact]
    public async Task RemovePluginFromAgent_ThrowsUnauthorizedException_WhenUserNotAuthenticated()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupUnauthenticatedUser();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.RemovePluginFromAgent(agentId, pluginId, CancellationToken.None));
    }

    #endregion

    #region DeletePlugin Tests

    [Fact]
    public async Task DeletePlugin_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.Is<DeletePluginCommand>(c => c.PluginId == pluginId && c.UserId == userId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        // Act
        var result = await _controller.DeletePlugin(pluginId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        var messageProp = response!.GetType().GetProperty("message");
        Assert.NotNull(messageProp);
        Assert.Equal("Plugin deleted successfully", messageProp.GetValue(response));
    }

    [Fact]
    public async Task DeletePlugin_CallsMediatorWithCorrectCommand()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<DeletePluginCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        // Act
        await _controller.DeletePlugin(pluginId, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeletePluginCommand>(c => c.PluginId == pluginId && c.UserId == userId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeletePlugin_ThrowsUnauthorizedException_WhenUserNotAuthenticated()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        SetupUnauthenticatedUser();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.DeletePlugin(pluginId, CancellationToken.None));
    }

    #endregion
}
