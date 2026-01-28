using System.Security.Claims;
using Application.UseCases.FeatureFlag;
using Application.UseCases.FeatureFlag.CreateFeatureFlag;
using Application.UseCases.FeatureFlag.DeleteFeatureFlag;
using Application.UseCases.FeatureFlag.GetFeatureFlagById;
using Application.UseCases.FeatureFlag.ListAllFeatureFlags;
using Application.UseCases.FeatureFlag.UpdateFeatureFlag;
using Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class AdminFeaturesControllerTests
{
    private readonly AdminFeaturesController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<AdminFeaturesController>> _loggerMock;

    public AdminFeaturesControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<AdminFeaturesController>>();
        _controller = new AdminFeaturesController(_mediatorMock.Object, _loggerMock.Object);
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

    #region GetAllFeatureFlags Tests

    [Fact]
    public async Task GetAllFeatureFlags_ReturnsOk_WithFeatureFlagList()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);
        var expectedFlags = new ListAllFeatureFlagsDto(new List<FeatureFlagDto>
        {
            new FeatureFlagDto(Guid.NewGuid(), "feature1", "Description 1", true, "Pro,Unlimited", 100, DateTime.UtcNow, DateTime.UtcNow),
            new FeatureFlagDto(Guid.NewGuid(), "feature2", "Description 2", false, null, 50, DateTime.UtcNow, DateTime.UtcNow)
        });

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListAllFeatureFlagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFlags);

        // Act
        var result = await _controller.GetAllFeatureFlags(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var flags = okResult.Value.Should().BeOfType<ListAllFeatureFlagsDto>().Subject;
        flags.FeatureFlags.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllFeatureFlags_Returns500_WhenExceptionOccurs()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListAllFeatureFlagsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetAllFeatureFlags(CancellationToken.None);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetFeatureFlagById Tests

    [Fact]
    public async Task GetFeatureFlagById_ReturnsOk_WhenFeatureFlagFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var flagId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var expectedFlag = new FeatureFlagDto(
            flagId,
            "test_feature",
            "Test description",
            true,
            "Pro",
            100,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetFeatureFlagByIdQuery>(q => q.Id == flagId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFlag);

        // Act
        var result = await _controller.GetFeatureFlagById(flagId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var flag = okResult.Value.Should().BeOfType<FeatureFlagDto>().Subject;
        flag.Id.Should().Be(flagId);
        flag.Key.Should().Be("test_feature");
    }

    [Fact]
    public async Task GetFeatureFlagById_ReturnsNotFound_WhenFeatureFlagNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var flagId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetFeatureFlagByIdQuery>(q => q.Id == flagId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FeatureFlagNotFoundException(flagId));

        // Act
        var result = await _controller.GetFeatureFlagById(flagId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region CreateFeatureFlag Tests

    [Fact]
    public async Task CreateFeatureFlag_ReturnsCreatedAtAction_WhenSuccessful()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new CreateFeatureFlagRequest(
            "new_feature",
            "New feature description",
            true,
            "Pro,Unlimited",
            100);

        var createdFlag = new FeatureFlagDto(
            Guid.NewGuid(),
            request.Key,
            request.Description,
            request.IsEnabled,
            request.AllowedPlans,
            request.RolloutPercentage,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(
                It.Is<CreateFeatureFlagCommand>(cmd =>
                    cmd.Key == request.Key &&
                    cmd.Description == request.Description &&
                    cmd.IsEnabled == request.IsEnabled &&
                    cmd.AllowedPlans == request.AllowedPlans &&
                    cmd.RolloutPercentage == request.RolloutPercentage),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdFlag);

        // Act
        var result = await _controller.CreateFeatureFlag(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AdminFeaturesController.GetFeatureFlagById));
        var flag = createdResult.Value.Should().BeOfType<FeatureFlagDto>().Subject;
        flag.Key.Should().Be(request.Key);
    }

    [Fact]
    public async Task CreateFeatureFlag_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new CreateFeatureFlagRequest(
            "",  // Invalid: empty key
            "Description",
            true,
            null,
            100);

        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateFeatureFlagCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Key cannot be empty"));

        // Act
        var result = await _controller.CreateFeatureFlag(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UpdateFeatureFlag Tests

    [Fact]
    public async Task UpdateFeatureFlag_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var flagId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new UpdateFeatureFlagRequest(
            "Updated description",
            false,
            "Free,Pro",
            75);

        var updatedFlag = new FeatureFlagDto(
            flagId,
            "existing_feature",
            request.Description,
            request.IsEnabled,
            request.AllowedPlans,
            request.RolloutPercentage,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(
                It.Is<UpdateFeatureFlagCommand>(cmd =>
                    cmd.Id == flagId &&
                    cmd.Description == request.Description &&
                    cmd.IsEnabled == request.IsEnabled &&
                    cmd.AllowedPlans == request.AllowedPlans &&
                    cmd.RolloutPercentage == request.RolloutPercentage),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedFlag);

        // Act
        var result = await _controller.UpdateFeatureFlag(flagId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var flag = okResult.Value.Should().BeOfType<FeatureFlagDto>().Subject;
        flag.Id.Should().Be(flagId);
        flag.Description.Should().Be(request.Description);
        flag.IsEnabled.Should().Be(request.IsEnabled);
    }

    [Fact]
    public async Task UpdateFeatureFlag_ReturnsNotFound_WhenFeatureFlagNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var flagId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new UpdateFeatureFlagRequest("Description", true, null, 100);

        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateFeatureFlagCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FeatureFlagNotFoundException(flagId));

        // Act
        var result = await _controller.UpdateFeatureFlag(flagId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateFeatureFlag_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var flagId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new UpdateFeatureFlagRequest("Description", true, null, 150);  // Invalid: > 100

        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateFeatureFlagCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("RolloutPercentage must be between 0 and 100"));

        // Act
        var result = await _controller.UpdateFeatureFlag(flagId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region DeleteFeatureFlag Tests

    [Fact]
    public async Task DeleteFeatureFlag_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var flagId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<DeleteFeatureFlagCommand>(cmd => cmd.Id == flagId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        // Act
        var result = await _controller.DeleteFeatureFlag(flagId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteFeatureFlag_ReturnsNotFound_WhenFeatureFlagNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var flagId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<DeleteFeatureFlagCommand>(cmd => cmd.Id == flagId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FeatureFlagNotFoundException(flagId));

        // Act
        var result = await _controller.DeleteFeatureFlag(flagId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion
}
