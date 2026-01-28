using System.Security.Claims;
using Application.UseCases.OpenApiPlugin.Common;
using Application.UseCases.OpenApiPlugin.CreateOpenApiPlugin;
using Application.UseCases.OpenApiPlugin.DeleteOpenApiPlugin;
using Application.UseCases.OpenApiPlugin.GetOpenApiPlugin;
using Application.UseCases.OpenApiPlugin.ListOpenApiPlugins;
using Application.UseCases.OpenApiPlugin.RefreshOpenApiPluginSpec;
using Application.UseCases.OpenApiPlugin.UpdateOpenApiPlugin;
using Application.UseCases.OpenApiPlugin.ValidateOpenApiSpec;
using Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class AdminOpenApiPluginsControllerTests
{
    private readonly AdminOpenApiPluginsController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<AdminOpenApiPluginsController>> _loggerMock;

    public AdminOpenApiPluginsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<AdminOpenApiPluginsController>>();
        _controller = new AdminOpenApiPluginsController(_mediatorMock.Object, _loggerMock.Object);
    }

    #region Helper Methods

    private void SetupAuthenticatedAdmin(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    #endregion

    #region GetAllPlugins Tests

    [Fact]
    public async Task GetAllPlugins_ReturnsOk_WithPluginList()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);
        var expectedPlugins = new List<OpenApiPluginDto>
        {
            new OpenApiPluginDto { Id = Guid.NewGuid(), Name = "Plugin 1", SystemName = "plugin1", BaseUrl = "https://api1.example.com", IsActive = true },
            new OpenApiPluginDto { Id = Guid.NewGuid(), Name = "Plugin 2", SystemName = "plugin2", BaseUrl = "https://api2.example.com", IsActive = false }
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListOpenApiPluginsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlugins);

        // Act
        var result = await _controller.GetAllPlugins(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var plugins = okResult.Value.Should().BeAssignableTo<IReadOnlyList<OpenApiPluginDto>>().Subject;
        plugins.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllPlugins_Returns500_WhenExceptionOccurs()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListOpenApiPluginsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetAllPlugins(CancellationToken.None);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetPluginById Tests

    [Fact]
    public async Task GetPluginById_ReturnsOk_WithPluginAndFunctions_WhenFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var expectedPlugin = new OpenApiPluginDto
        {
            Id = pluginId,
            Name = "Test Plugin",
            SystemName = "test_plugin",
            BaseUrl = "https://api.example.com",
            IsActive = true,
            AvailableFunctions = new List<OpenApiFunctionDto>
            {
                new OpenApiFunctionDto("getUser", "Get user by ID", "GET", "/users/{id}"),
                new OpenApiFunctionDto("createUser", "Create a new user", "POST", "/users")
            }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetOpenApiPluginQuery>(q => q.Id == pluginId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlugin);

        // Act
        var result = await _controller.GetPluginById(pluginId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var plugin = okResult.Value.Should().BeOfType<OpenApiPluginDto>().Subject;
        plugin.Id.Should().Be(pluginId);
        plugin.AvailableFunctions.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPluginById_ReturnsNotFound_WhenPluginNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetOpenApiPluginQuery>(q => q.Id == pluginId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PluginNotFoundException(pluginId));

        // Act
        var result = await _controller.GetPluginById(pluginId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPluginById_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetOpenApiPluginQuery>(q => q.Id == pluginId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Invalid OpenAPI spec"));

        // Act
        var result = await _controller.GetPluginById(pluginId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region CreatePlugin Tests

    [Fact]
    public async Task CreatePlugin_ReturnsCreatedAtAction_WhenSuccessful()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new CreateOpenApiPluginRequest(
            "New Plugin",
            "new_plugin",
            "https://api.newplugin.com",
            "A new plugin description",
            "/openapi.json",
            30,
            10,
            false,
            true);

        var createdPlugin = new OpenApiPluginDto
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            SystemName = request.SystemName,
            BaseUrl = request.BaseUrl,
            Description = request.Description,
            OpenApiSpecUrl = request.OpenApiSpecUrl,
            IsActive = request.IsActive,
            PriceCredits = request.PriceCredits,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<CreateOpenApiPluginCommand>(cmd =>
                    cmd.Name == request.Name &&
                    cmd.SystemName == request.SystemName &&
                    cmd.BaseUrl == request.BaseUrl),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPlugin);

        // Act
        var result = await _controller.CreatePlugin(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AdminOpenApiPluginsController.GetPluginById));
        var plugin = createdResult.Value.Should().BeOfType<OpenApiPluginDto>().Subject;
        plugin.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task CreatePlugin_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new CreateOpenApiPluginRequest(
            "",  // Invalid: empty name
            "new_plugin",
            "https://api.example.com");

        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateOpenApiPluginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Name cannot be empty", "Name"));

        // Act
        var result = await _controller.CreatePlugin(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UpdatePlugin Tests

    [Fact]
    public async Task UpdatePlugin_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new UpdateOpenApiPluginRequest(
            "Updated Plugin",
            "Updated description",
            "https://api.updated.com",
            "/v2/openapi.json",
            60,
            20,
            true,
            true);

        var updatedPlugin = new OpenApiPluginDto
        {
            Id = pluginId,
            Name = request.Name!,
            Description = request.Description,
            BaseUrl = request.BaseUrl!,
            IsActive = request.IsActive ?? false,
            UpdatedAt = DateTime.UtcNow
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<UpdateOpenApiPluginCommand>(cmd =>
                    cmd.Id == pluginId &&
                    cmd.Name == request.Name),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedPlugin);

        // Act
        var result = await _controller.UpdatePlugin(pluginId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var plugin = okResult.Value.Should().BeOfType<OpenApiPluginDto>().Subject;
        plugin.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task UpdatePlugin_ReturnsNotFound_WhenPluginNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new UpdateOpenApiPluginRequest("Updated Name");

        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateOpenApiPluginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PluginNotFoundException(pluginId));

        // Act
        var result = await _controller.UpdatePlugin(pluginId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeletePlugin Tests

    [Fact]
    public async Task DeletePlugin_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<DeleteOpenApiPluginCommand>(cmd => cmd.Id == pluginId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        // Act
        var result = await _controller.DeletePlugin(pluginId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeletePlugin_ReturnsNotFound_WhenPluginNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<DeleteOpenApiPluginCommand>(cmd => cmd.Id == pluginId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PluginNotFoundException(pluginId));

        // Act
        var result = await _controller.DeletePlugin(pluginId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region RefreshSpec Tests

    [Fact]
    public async Task RefreshSpec_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var refreshedPlugin = new OpenApiPluginDto
        {
            Id = pluginId,
            Name = "Test Plugin",
            SpecCachedAt = DateTime.UtcNow,
            AvailableFunctions = new List<OpenApiFunctionDto>
            {
                new OpenApiFunctionDto("operation1", "Description 1", "GET", "/api/v1/test")
            }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<RefreshOpenApiPluginSpecCommand>(cmd => cmd.Id == pluginId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshedPlugin);

        // Act
        var result = await _controller.RefreshSpec(pluginId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var plugin = okResult.Value.Should().BeOfType<OpenApiPluginDto>().Subject;
        plugin.SpecCachedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshSpec_ReturnsNotFound_WhenPluginNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<RefreshOpenApiPluginSpecCommand>(cmd => cmd.Id == pluginId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PluginNotFoundException(pluginId));

        // Act
        var result = await _controller.RefreshSpec(pluginId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RefreshSpec_ReturnsBadRequest_WhenSpecInvalid()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<RefreshOpenApiPluginSpecCommand>(cmd => cmd.Id == pluginId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Invalid OpenAPI spec: missing paths"));

        // Act
        var result = await _controller.RefreshSpec(pluginId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region ValidateSpec Tests

    [Fact]
    public async Task ValidateSpec_ReturnsOk_WhenSpecValid()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new ValidateOpenApiSpecRequest(
            "https://api.example.com",
            "/openapi.json");

        var validationResult = new OpenApiSpecValidationResultDto(
            true,
            "Example API",
            "1.0.0",
            new List<OpenApiFunctionDto>
            {
                new OpenApiFunctionDto("getItems", "Get all items", "GET", "/items"),
                new OpenApiFunctionDto("createItem", "Create item", "POST", "/items")
            },
            new List<string>(),
            new List<string>());

        _mediatorMock.Setup(m => m.Send(
                It.Is<ValidateOpenApiSpecQuery>(q =>
                    q.BaseUrl == request.BaseUrl &&
                    q.OpenApiSpecUrl == request.OpenApiSpecUrl),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateSpec(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var validation = okResult.Value.Should().BeOfType<OpenApiSpecValidationResultDto>().Subject;
        validation.IsValid.Should().BeTrue();
        validation.DiscoveredOperations.Should().HaveCount(2);
    }

    [Fact]
    public async Task ValidateSpec_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new ValidateOpenApiSpecRequest(
            "invalid-url",  // Invalid URL
            "/openapi.json");

        _mediatorMock.Setup(m => m.Send(It.IsAny<ValidateOpenApiSpecQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Invalid base URL format", "BaseUrl"));

        // Act
        var result = await _controller.ValidateSpec(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
