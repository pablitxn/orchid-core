using System.Security.Claims;
using Application.Interfaces;
using Application.UseCases.FeatureFlag.ListFeatureFlags;
using Domain.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class FeaturesControllerTests
{
    private readonly FeaturesController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;

    public FeaturesControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _controller = new FeaturesController(_mediatorMock.Object, _subscriptionRepositoryMock.Object);
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

    #region GetFeatures Tests

    [Fact]
    public async Task GetFeatures_ReturnsOk_WhenUnauthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var expectedFeatures = new Dictionary<string, bool>
        {
            { "feature1", true },
            { "feature2", false }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListFeatureFlagsQuery>(q => q.UserId == null && q.UserPlan == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListFeatureFlagsDto(expectedFeatures));

        // Act
        var result = await _controller.GetFeatures(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value;
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFeatures_ReturnsOk_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var expectedFeatures = new Dictionary<string, bool>
        {
            { "premium_feature", true },
            { "beta_feature", false }
        };

        var subscription = new SubscriptionEntity
        {
            UserId = userId,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = Domain.Enums.SubscriptionPlanEnum.Monthly100,
                Price = 9.99m,
                Credits = 100
            }
        };

        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListFeatureFlagsQuery>(q => q.UserId == userId && q.UserPlan == "Monthly100"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListFeatureFlagsDto(expectedFeatures));

        // Act
        var result = await _controller.GetFeatures(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFeatures_SendsCorrectQuery_WhenAuthenticatedWithoutSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListFeatureFlagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListFeatureFlagsDto(new Dictionary<string, bool>()));

        // Act
        await _controller.GetFeatures(CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ListFeatureFlagsQuery>(q => q.UserId == userId && q.UserPlan == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetFeature Tests

    [Fact]
    public async Task GetFeature_ReturnsOk_WithFeatureEnabled_WhenFeatureExists()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var featureKey = "test_feature";
        var expectedFeatures = new Dictionary<string, bool>
        {
            { featureKey, true }
        };

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<ListFeatureFlagsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListFeatureFlagsDto(expectedFeatures));

        // Act
        var result = await _controller.GetFeature(featureKey, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFeature_ReturnsOk_WithFeatureDisabled_WhenFeatureNotFound()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var featureKey = "nonexistent_feature";
        var expectedFeatures = new Dictionary<string, bool>
        {
            { "other_feature", true }
        };

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<ListFeatureFlagsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListFeatureFlagsDto(expectedFeatures));

        // Act
        var result = await _controller.GetFeature(featureKey, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFeature_UsesUserContext_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var featureKey = "premium_feature";

        var subscription = new SubscriptionEntity
        {
            UserId = userId,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = Domain.Enums.SubscriptionPlanEnum.Unlimited,
                Price = 49.99m,
                Credits = 1000
            }
        };

        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListFeatureFlagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListFeatureFlagsDto(new Dictionary<string, bool> { { featureKey, true } }));

        // Act
        await _controller.GetFeature(featureKey, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ListFeatureFlagsQuery>(q => q.UserId == userId && q.UserPlan == nameof(Domain.Enums.SubscriptionPlanEnum.Unlimited)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
