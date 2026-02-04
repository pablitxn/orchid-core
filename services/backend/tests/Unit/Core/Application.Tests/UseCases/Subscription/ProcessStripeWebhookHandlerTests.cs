using Application.Interfaces;
using Application.UseCases.Subscription.ProcessWebhook;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.Subscription;

public class ProcessStripeWebhookHandlerTests
{
    private readonly ProcessStripeWebhookHandler _handler;
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IStripeWebhookEventRepository> _webhookRepo = new();
    private readonly Mock<ISubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<ISubscriptionPlanRepository> _planRepo = new();
    private readonly Mock<IUserCreditsRepository> _userCreditsRepo = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ICreditPurchaseRepository> _creditPurchaseRepo = new();
    private readonly Mock<ILogger<ProcessStripeWebhookHandler>> _logger = new();

    public ProcessStripeWebhookHandlerTests()
    {
        _handler = new ProcessStripeWebhookHandler(
            _stripeService.Object,
            _webhookRepo.Object,
            _subscriptionRepo.Object,
            _planRepo.Object,
            _userCreditsRepo.Object,
            _creditPurchaseRepo.Object,
            _eventPublisher.Object,
            _notificationService.Object,
            _logger.Object);

        _userCreditsRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        _userCreditsRepo
            .Setup(r => r.CreateAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity uc, CancellationToken _) => uc);

        _userCreditsRepo
            .Setup(r => r.UpdateAsync(It.IsAny<UserCreditsEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_InvalidSignature_ReturnsFalse()
    {
        _stripeService.Setup(s => s.ValidateAndParseWebhook(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((StripeWebhookEvent?)null);

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "invalid-sig"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid signature", result.Message);
    }

    [Fact]
    public async Task Handle_DuplicateEvent_ReturnsTrue()
    {
        var eventId = "evt_123";
        _stripeService.Setup(s => s.ValidateAndParseWebhook(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new StripeWebhookEvent(eventId, "test.event", CreateEmptyWebhookData()));

        _webhookRepo.Setup(r => r.ExistsAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "sig"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Duplicate event", result.Message);
        Assert.Equal(eventId, result.EventId);
    }

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_LinksSubscriptionToStripe()
    {
        var eventId = "evt_checkout_123";
        var customerId = "cus_123";
        var stripeSubId = "sub_456";
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StripeCustomerId = customerId
        };

        SetupValidWebhookEvent(eventId, "checkout.session.completed",
            new StripeWebhookData(customerId, stripeSubId, null, null, null, null, false, null, null));

        _subscriptionRepo.Setup(r => r.GetByStripeCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _stripeService.Setup(s => s.GetSubscriptionAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSubscriptionInfo(
                stripeSubId, customerId, "active", "price_123",
                DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), false, null, null));

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "sig"),
            CancellationToken.None);

        Assert.True(result.Success);
        _subscriptionRepo.Verify(r => r.UpdateAsync(
            It.Is<SubscriptionEntity>(s => s.StripeSubscriptionId == stripeSubId),
            It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(e => e.PublishAsync(It.IsAny<SubscriptionUpdatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SubscriptionUpdated_UpdatesSubscriptionStatus()
    {
        var eventId = "evt_sub_update_123";
        var stripeSubId = "sub_456";
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StripeSubscriptionId = stripeSubId,
            Status = SubscriptionStatus.Incomplete
        };

        SetupValidWebhookEvent(eventId, "customer.subscription.updated",
            new StripeWebhookData("cus_123", stripeSubId, "price_123", "active",
                DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), false, null, null));

        _subscriptionRepo.Setup(r => r.GetByStripeSubscriptionIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "sig"),
            CancellationToken.None);

        Assert.True(result.Success);
        _subscriptionRepo.Verify(r => r.UpdateAsync(
            It.Is<SubscriptionEntity>(s => s.Status == SubscriptionStatus.Active),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SubscriptionDeleted_MarksAsCanceled()
    {
        var eventId = "evt_sub_del_123";
        var stripeSubId = "sub_456";
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StripeSubscriptionId = stripeSubId,
            Status = SubscriptionStatus.Active
        };

        SetupValidWebhookEvent(eventId, "customer.subscription.deleted",
            new StripeWebhookData("cus_123", stripeSubId, null, "canceled",
                DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), true, DateTime.UtcNow, null));

        _subscriptionRepo.Setup(r => r.GetByStripeSubscriptionIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "sig"),
            CancellationToken.None);

        Assert.True(result.Success);
        _subscriptionRepo.Verify(r => r.UpdateAsync(
            It.Is<SubscriptionEntity>(s => s.Status == SubscriptionStatus.Canceled),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationService.Verify(n => n.SendSubscriptionExpiredAlertAsync(
            subscription.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvoicePaid_RefreshesCredits()
    {
        var eventId = "evt_invoice_123";
        var stripeSubId = "sub_456";
        var plan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            Credits = 100,
            Price = 29.99m,
            PlanEnum = SubscriptionPlanEnum.Monthly100
        };
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StripeSubscriptionId = stripeSubId,
            SubscriptionPlan = plan
        };

        SetupValidWebhookEvent(eventId, "invoice.paid",
            new StripeWebhookData("cus_123", stripeSubId, null, "paid", null, null, false, null, null));

        _subscriptionRepo.Setup(r => r.GetByStripeSubscriptionIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "sig"),
            CancellationToken.None);

        Assert.True(result.Success);
        _subscriptionRepo.Verify(r => r.UpdateAsync(
            It.IsAny<SubscriptionEntity>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _userCreditsRepo.Verify(r => r.CreateAsync(
            It.Is<UserCreditsEntity>(uc => uc.UserId == subscription.UserId && uc.AvailableCredits == 100),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvoicePaymentFailed_MarksPastDue()
    {
        var eventId = "evt_invoice_fail_123";
        var stripeSubId = "sub_456";
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StripeSubscriptionId = stripeSubId,
            Status = SubscriptionStatus.Active
        };

        SetupValidWebhookEvent(eventId, "invoice.payment_failed",
            new StripeWebhookData("cus_123", stripeSubId, null, "open", null, null, false, null, null));

        _subscriptionRepo.Setup(r => r.GetByStripeSubscriptionIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "sig"),
            CancellationToken.None);

        Assert.True(result.Success);
        _subscriptionRepo.Verify(r => r.UpdateAsync(
            It.Is<SubscriptionEntity>(s => s.Status == SubscriptionStatus.PastDue),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationService.Verify(n => n.SendSubscriptionExpiringAlertAsync(
            subscription.UserId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PaymentIntentSucceeded_ReactivatesPastDueSubscription()
    {
        var eventId = "evt_pi_success_123";
        var customerId = "cus_123";
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StripeCustomerId = customerId,
            Status = SubscriptionStatus.PastDue
        };

        SetupValidWebhookEvent(eventId, "payment_intent.succeeded",
            new StripeWebhookData(customerId, null, null, "succeeded", null, null, false, null, null,
                "pi_123", 2000, "usd", null));

        _subscriptionRepo.Setup(r => r.GetByStripeCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "sig"),
            CancellationToken.None);

        Assert.True(result.Success);
        _subscriptionRepo.Verify(r => r.UpdateAsync(
            It.Is<SubscriptionEntity>(s => s.Status == SubscriptionStatus.Active),
            It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(e => e.PublishAsync(It.IsAny<SubscriptionUpdatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PaymentIntentFailed_MarksPastDue()
    {
        var eventId = "evt_pi_fail_123";
        var customerId = "cus_123";
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StripeCustomerId = customerId,
            Status = SubscriptionStatus.Active
        };

        SetupValidWebhookEvent(eventId, "payment_intent.payment_failed",
            new StripeWebhookData(customerId, null, null, "requires_payment_method", null, null, false, null, null,
                "pi_123", 0, "usd", null));

        _subscriptionRepo.Setup(r => r.GetByStripeCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "sig"),
            CancellationToken.None);

        Assert.True(result.Success);
        _subscriptionRepo.Verify(r => r.UpdateAsync(
            It.Is<SubscriptionEntity>(s => s.Status == SubscriptionStatus.PastDue),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationService.Verify(n => n.SendSubscriptionExpiringAlertAsync(
            subscription.UserId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProcessingError_SchedulesRetry()
    {
        var eventId = "evt_error_123";

        SetupValidWebhookEvent(eventId, "customer.subscription.updated",
            new StripeWebhookData("cus_123", "sub_456", null, "active", null, null, false, null, null));

        _subscriptionRepo.Setup(r => r.GetByStripeSubscriptionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _handler.Handle(
            new ProcessStripeWebhookCommand("payload", "sig"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Database error", result.Message);
        _webhookRepo.Verify(r => r.UpdateAsync(
            It.Is<StripeWebhookEventEntity>(e =>
                e.Status == WebhookEventStatus.Failed &&
                e.NextRetryAt != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupValidWebhookEvent(string eventId, string eventType, StripeWebhookData data)
    {
        _stripeService.Setup(s => s.ValidateAndParseWebhook(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new StripeWebhookEvent(eventId, eventType, data));

        _webhookRepo.Setup(r => r.ExistsAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _webhookRepo.Setup(r => r.CreateAsync(It.IsAny<StripeWebhookEventEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StripeWebhookEventEntity e, CancellationToken _) => e);
    }

    private static StripeWebhookData CreateEmptyWebhookData()
    {
        return new StripeWebhookData(null, null, null, null, null, null, false, null, null);
    }
}
