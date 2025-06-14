using Application.Interfaces;
using Application.UseCases.Plugin.ExecutePlugin;
using Domain.Entities;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Application.Tests.UseCases.Plugin;

public class ExecutePluginHandlerTests
{
    private readonly Mock<IPluginRepository> _pluginRepo = new();
    private readonly Mock<IUserPluginRepository> _userPluginRepo = new();
    private readonly Mock<ISubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<ICreditTrackingService> _tracking = new();
    private readonly Mock<ICreditLimitService> _limitService = new();
    private readonly Mock<IPluginDiscoveryService> _discovery = new();
    private readonly Mock<ICostRegistry> _costRegistry = new();
    private readonly Mock<ILogger<ExecutePluginHandler>> _logger = new();
    private readonly ExecutePluginHandler _handler;

    public ExecutePluginHandlerTests()
    {
        _handler = new ExecutePluginHandler(
            _pluginRepo.Object,
            _userPluginRepo.Object,
            _subscriptionRepo.Object,
            _tracking.Object,
            _limitService.Object,
            _discovery.Object,
            _costRegistry.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLimitExceeded()
    {
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var cmd = new ExecutePluginCommand(userId, pluginId, "{}");

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test",
            IsActive = true,
            IsSubscriptionBased = false
        };
        var subscription = new SubscriptionEntity { UserId = userId, Credits = 10 };

        _userPluginRepo.Setup(r => r.GetByUserAndPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPluginEntity { UserId = userId, PluginId = pluginId, IsActive = true });
        _pluginRepo.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);
        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _costRegistry.Setup(c => c.GetPluginUsageCostAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _tracking.Setup(t => t.ValidateSufficientCreditsAsync(userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _limitService.Setup(x => x.CheckLimitsAsync(userId, 1, "plugin_usage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(false));

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.Success);
        _subscriptionRepo.Verify(r => r.UpdateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _limitService.Verify(x => x.ConsumeLimitsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
