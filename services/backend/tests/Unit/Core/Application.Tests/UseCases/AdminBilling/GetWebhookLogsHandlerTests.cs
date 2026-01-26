using Application.Interfaces;
using Application.UseCases.AdminBilling.GetWebhookLogs;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.AdminBilling;

public class GetWebhookLogsHandlerTests
{
    private readonly Mock<IStripeWebhookEventRepository> _repo = new();
    private readonly Mock<ILogger<GetWebhookLogsHandler>> _logger = new();
    private readonly GetWebhookLogsHandler _handler;

    public GetWebhookLogsHandlerTests()
    {
        _handler = new GetWebhookLogsHandler(_repo.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsWebhookLogsWithStats()
    {
        var events = new List<StripeWebhookEventEntity>
        {
            new() { Id = Guid.NewGuid(), StripeEventId = "evt_1", EventType = "checkout.session.completed", Payload = "{}", Status = WebhookEventStatus.Processed },
            new() { Id = Guid.NewGuid(), StripeEventId = "evt_2", EventType = "invoice.paid", Payload = "{}", Status = WebhookEventStatus.Failed }
        };

        var stats = new WebhookEventStats(10, 8, 1, 1, 0);

        _repo.Setup(r => r.GetAllAsync(null, null, null, null, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((events.AsEnumerable(), 2));
        _repo.Setup(r => r.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var query = new GetWebhookLogsQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(10, result.Stats.TotalEvents);
        Assert.Equal(8, result.Stats.ProcessedEvents);
    }

    [Fact]
    public async Task Handle_FiltersbyStatus()
    {
        var events = new List<StripeWebhookEventEntity>
        {
            new() { Id = Guid.NewGuid(), StripeEventId = "evt_1", EventType = "invoice.paid", Payload = "{}", Status = WebhookEventStatus.Failed }
        };

        var stats = new WebhookEventStats(10, 8, 1, 1, 0);

        _repo.Setup(r => r.GetAllAsync(WebhookEventStatus.Failed, null, null, null, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((events.AsEnumerable(), 1));
        _repo.Setup(r => r.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var query = new GetWebhookLogsQuery(Status: WebhookEventStatus.Failed);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(WebhookEventStatus.Failed, result.Items[0].Status);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoEvents()
    {
        var stats = new WebhookEventStats(0, 0, 0, 0, 0);

        _repo.Setup(r => r.GetAllAsync(null, null, null, null, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<StripeWebhookEventEntity>(), 0));
        _repo.Setup(r => r.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var query = new GetWebhookLogsQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
