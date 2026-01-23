using System.Security.Claims;
using Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class CreditValidationControllerTests
{
    private readonly CreditValidationController _controller;
    private readonly Mock<ICreditValidationService> _creditValidationServiceMock;
    private readonly Mock<ILogger<CreditValidationController>> _loggerMock;

    public CreditValidationControllerTests()
    {
        _creditValidationServiceMock = new Mock<ICreditValidationService>();
        _loggerMock = new Mock<ILogger<CreditValidationController>>();
        _controller = new CreditValidationController(_creditValidationServiceMock.Object, _loggerMock.Object);
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

    private void SetupUserWithInvalidGuid()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "not-a-valid-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    #endregion

    #region ValidateMessageCost Tests

    [Fact]
    public async Task ValidateMessageCost_ReturnsOk_WhenValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new ValidateMessageRequest("Hello", null, null);
        var validationResult = new CreditValidationResult(
            IsValid: true,
            RequiredCredits: 1,
            AvailableCredits: 100,
            HasUnlimitedCredits: false);

        _creditValidationServiceMock.Setup(s => s.ValidateMessageCostAsync(
            userId,
            request.MessageContent,
            request.PluginIds,
            request.WorkflowIds,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateMessageCost(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateMessageCost_ReturnsOk_WithCostBreakdown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var pluginIds = new List<Guid> { Guid.NewGuid() };
        var workflowIds = new List<Guid> { Guid.NewGuid() };
        var request = new ValidateMessageRequest("Hello", pluginIds, workflowIds);
        var costBreakdown = new CostBreakdown(
            BaseCost: 1,
            PluginCosts: new Dictionary<string, int> { { "plugin1", 5 } },
            WorkflowCosts: new Dictionary<string, int> { { "workflow1", 10 } },
            TotalCost: 16);
        var validationResult = new CreditValidationResult(
            IsValid: true,
            RequiredCredits: 16,
            AvailableCredits: 100,
            HasUnlimitedCredits: false,
            CostBreakdown: costBreakdown);

        _creditValidationServiceMock.Setup(s => s.ValidateMessageCostAsync(
            userId,
            request.MessageContent,
            request.PluginIds,
            request.WorkflowIds,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateMessageCost(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateMessageCost_ReturnsOk_WhenInsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new ValidateMessageRequest("Hello", null, null);
        var validationResult = new CreditValidationResult(
            IsValid: false,
            RequiredCredits: 10,
            AvailableCredits: 5,
            HasUnlimitedCredits: false,
            ErrorMessage: "Insufficient credits");

        _creditValidationServiceMock.Setup(s => s.ValidateMessageCostAsync(
            userId,
            request.MessageContent,
            request.PluginIds,
            request.WorkflowIds,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateMessageCost(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateMessageCost_ThrowsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var request = new ValidateMessageRequest("Hello", null, null);

        // Act
        var act = () => _controller.ValidateMessageCost(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ValidateMessageCost_ThrowsUnauthorized_WhenInvalidGuid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var request = new ValidateMessageRequest("Hello", null, null);

        // Act
        var act = () => _controller.ValidateMessageCost(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region ValidatePluginPurchase Tests

    [Fact]
    public async Task ValidatePluginPurchase_ReturnsOk_WhenValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var validationResult = new CreditValidationResult(
            IsValid: true,
            RequiredCredits: 10,
            AvailableCredits: 100,
            HasUnlimitedCredits: false);

        _creditValidationServiceMock.Setup(s => s.ValidatePluginPurchaseAsync(
            userId,
            pluginId,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidatePluginPurchase(pluginId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidatePluginPurchase_ReturnsOk_WhenInsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var validationResult = new CreditValidationResult(
            IsValid: false,
            RequiredCredits: 100,
            AvailableCredits: 10,
            HasUnlimitedCredits: false,
            ErrorMessage: "Insufficient credits for plugin purchase");

        _creditValidationServiceMock.Setup(s => s.ValidatePluginPurchaseAsync(
            userId,
            pluginId,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidatePluginPurchase(pluginId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidatePluginPurchase_ThrowsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var pluginId = Guid.NewGuid();

        // Act
        var act = () => _controller.ValidatePluginPurchase(pluginId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region ValidateWorkflowPurchase Tests

    [Fact]
    public async Task ValidateWorkflowPurchase_ReturnsOk_WhenValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var validationResult = new CreditValidationResult(
            IsValid: true,
            RequiredCredits: 20,
            AvailableCredits: 100,
            HasUnlimitedCredits: false);

        _creditValidationServiceMock.Setup(s => s.ValidateWorkflowPurchaseAsync(
            userId,
            workflowId,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateWorkflowPurchase(workflowId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateWorkflowPurchase_ReturnsOk_WhenInsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var validationResult = new CreditValidationResult(
            IsValid: false,
            RequiredCredits: 50,
            AvailableCredits: 10,
            HasUnlimitedCredits: false,
            ErrorMessage: "Insufficient credits for workflow purchase");

        _creditValidationServiceMock.Setup(s => s.ValidateWorkflowPurchaseAsync(
            userId,
            workflowId,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateWorkflowPurchase(workflowId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateWorkflowPurchase_ThrowsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var workflowId = Guid.NewGuid();

        // Act
        var act = () => _controller.ValidateWorkflowPurchase(workflowId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region ValidateOperation Tests

    [Fact]
    public async Task ValidateOperation_ReturnsOk_WhenValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new ValidateOperationRequest(10, "test_operation");
        var validationResult = new CreditValidationResult(
            IsValid: true,
            RequiredCredits: 10,
            AvailableCredits: 100,
            HasUnlimitedCredits: false);

        _creditValidationServiceMock.Setup(s => s.ValidateOperationAsync(
            userId,
            request.RequiredCredits,
            request.OperationType,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateOperation(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateOperation_ReturnsOk_WhenInsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new ValidateOperationRequest(100, "expensive_operation");
        var validationResult = new CreditValidationResult(
            IsValid: false,
            RequiredCredits: 100,
            AvailableCredits: 50,
            HasUnlimitedCredits: false,
            ErrorMessage: "Insufficient credits for operation");

        _creditValidationServiceMock.Setup(s => s.ValidateOperationAsync(
            userId,
            request.RequiredCredits,
            request.OperationType,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateOperation(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateOperation_ReturnsOk_WhenUnlimitedCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new ValidateOperationRequest(100, "test_operation");
        var validationResult = new CreditValidationResult(
            IsValid: true,
            RequiredCredits: 100,
            AvailableCredits: 0,
            HasUnlimitedCredits: true);

        _creditValidationServiceMock.Setup(s => s.ValidateOperationAsync(
            userId,
            request.RequiredCredits,
            request.OperationType,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateOperation(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateOperation_ThrowsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var request = new ValidateOperationRequest(10, "test_operation");

        // Act
        var act = () => _controller.ValidateOperation(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ValidateOperation_ThrowsUnauthorized_WhenInvalidGuid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var request = new ValidateOperationRequest(10, "test_operation");

        // Act
        var act = () => _controller.ValidateOperation(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion
}
