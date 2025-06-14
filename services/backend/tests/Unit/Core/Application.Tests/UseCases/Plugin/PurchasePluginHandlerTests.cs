using Application.Interfaces;
using Application.UseCases.Plugin.PurchasePlugin;
using Domain.Entities;
using Moq;
using Microsoft.Extensions.Logging;

namespace Application.Tests.UseCases.Plugin;

public class PurchasePluginHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPluginRepository> _pluginRepo = new();
    private readonly Mock<IUserPluginRepository> _userPluginRepo = new();
    private readonly Mock<ISubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<ICreditTrackingService> _tracking = new();
    private readonly Mock<ICostRegistry> _costRegistry = new();
    private readonly Mock<ICreditLimitService> _limitService = new();
    private readonly Mock<ILogger<PurchasePluginHandler>> _logger = new();
    private readonly PurchasePluginHandler _handler;

    public PurchasePluginHandlerTests()
    {
        // Setup UnitOfWork to return the mocked repositories
        _unitOfWork.Setup(uow => uow.Plugins).Returns(_pluginRepo.Object);
        _unitOfWork.Setup(uow => uow.UserPlugins).Returns(_userPluginRepo.Object);
        _unitOfWork.Setup(uow => uow.Subscriptions).Returns(_subscriptionRepo.Object);

        _handler = new PurchasePluginHandler(
            _unitOfWork.Object,
            _tracking.Object,
            _costRegistry.Object,
            _limitService.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLimitExceeded()
    {
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var cmd = new PurchasePluginCommand(userId, pluginId);

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test",
            IsActive = true,
            IsSubscriptionBased = false
        };
        var subscription = new SubscriptionEntity { UserId = userId, Credits = 100 };

        _pluginRepo.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);
        _userPluginRepo.Setup(r => r.GetByUserAndPluginAsync(userId, pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPluginEntity?)null);
        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _costRegistry.Setup(r => r.GetPluginPurchaseCostAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);
        _limitService.Setup(x => x.CheckLimitsAsync(userId, 50, "plugin_purchase", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditLimitCheckResult(false));

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.Success);
        _subscriptionRepo.Verify(r => r.UpdateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _limitService.Verify(x => x.ConsumeLimitsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
