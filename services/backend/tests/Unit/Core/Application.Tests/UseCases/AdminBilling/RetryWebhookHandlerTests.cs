using Application.Interfaces;
using Application.UseCases.AdminBilling.RetryWebhook;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.AdminBilling;

public class RetryWebhookHandlerTests
{
    private readonly Mock<IStripeWebhookEventRepository> _webhookRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<ISubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<ISubscriptionPlanRepository> _planRepo = new();
    private readonly Mock<ICreditPurchaseRepository> _creditPurchaseRepo = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<ILogger<RetryWebhookHandler>> _logger = new();
    private readonly RetryWebhookHandler _handler;

    public RetryWebhookHandlerTests()
    {
        _handler = new RetryWebhookHandler(
            _webhookRepo.Object,
            _stripeService.Object,
            _subscriptionRepo.Object,
            _planRepo.Object,
            _creditPurchaseRepo.Object,
            _eventPublisher.Object,
            _notificationService.Object,
            _auditService.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_RetriesFailedWebhook_Successfully()
    {
        var webhookId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var webhookEvent = new StripeWebhookEventEntity
        {
            Id = webhookId,
            StripeEventId = "evt_test",
            EventType = "checkout.session.completed",
            Payload = "{}",
            Status = WebhookEventStatus.Failed
        };

        var parsedEvent = new StripeWebhookEvent(
            "evt_test",
            "checkout.session.completed",
            new StripeWebhookData(null, null, null, null, null, null, false, null, null));

        _webhookRepo.Setup(r => r.GetByIdAsync(webhookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);
        _stripeService.Setup(s => s.ParseWebhookPayload(It.IsAny<string>()))
            .Returns(parsedEvent);

        var cmd = new RetryWebhookCommand(webhookId, adminId);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(webhookId, result.Id);
        Assert.Equal("evt_test", result.StripeEventId);
        _webhookRepo.Verify(r => r.UpdateAsync(It.IsAny<StripeWebhookEventEntity>(), It.IsAny<CancellationToken>()), Times.AtLeast(1));
        _auditService.Verify(a => a.LogSuccessAsync(
            AuditActionTypes.AdminWebhookRetry,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFoundException_WhenWebhookNotFound()
    {
        var webhookId = Guid.NewGuid();
        _webhookRepo.Setup(r => r.GetByIdAsync(webhookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StripeWebhookEventEntity?)null);

        var cmd = new RetryWebhookCommand(webhookId, Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenWebhookAlreadyProcessed()
    {
        var webhookId = Guid.NewGuid();
        var webhookEvent = new StripeWebhookEventEntity
        {
            Id = webhookId,
            StripeEventId = "evt_test",
            EventType = "checkout.session.completed",
            Payload = "{}",
            Status = WebhookEventStatus.Processed
        };

        _webhookRepo.Setup(r => r.GetByIdAsync(webhookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);

        var cmd = new RetryWebhookCommand(webhookId, Guid.NewGuid());

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CanRetryDeadLetterWebhook()
    {
        var webhookId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var webhookEvent = new StripeWebhookEventEntity
        {
            Id = webhookId,
            StripeEventId = "evt_test",
            EventType = "invoice.paid",
            Payload = "{}",
            Status = WebhookEventStatus.DeadLetter
        };

        var parsedEvent = new StripeWebhookEvent(
            "evt_test",
            "invoice.paid",
            new StripeWebhookData(null, null, null, null, null, null, false, null, null));

        _webhookRepo.Setup(r => r.GetByIdAsync(webhookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);
        _stripeService.Setup(s => s.ParseWebhookPayload(It.IsAny<string>()))
            .Returns(parsedEvent);

        var cmd = new RetryWebhookCommand(webhookId, adminId);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Handle_ThrowsOperationFailedException_WhenPayloadCannotBeParsed()
    {
        var webhookId = Guid.NewGuid();
        var webhookEvent = new StripeWebhookEventEntity
        {
            Id = webhookId,
            StripeEventId = "evt_test",
            EventType = "checkout.session.completed",
            Payload = "invalid",
            Status = WebhookEventStatus.Failed
        };

        _webhookRepo.Setup(r => r.GetByIdAsync(webhookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);
        _stripeService.Setup(s => s.ParseWebhookPayload(It.IsAny<string>()))
            .Returns((StripeWebhookEvent?)null);

        var cmd = new RetryWebhookCommand(webhookId, Guid.NewGuid());

        await Assert.ThrowsAsync<OperationFailedException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }
}
