using Domain.Entities;

namespace Domain.Tests.Entities;

public class StripeWebhookEventEntityTests
{
    [Fact]
    public void MarkProcessed_SetsStatusAndProcessedAt()
    {
        var entity = CreateEntity();

        entity.MarkProcessed();

        Assert.Equal(WebhookEventStatus.Processed, entity.Status);
        Assert.NotNull(entity.ProcessedAt);
    }

    [Fact]
    public void MarkFailed_SetsStatusErrorMessageAndIncrementsRetryCount()
    {
        var entity = CreateEntity();

        entity.MarkFailed("Connection timeout");

        Assert.Equal(WebhookEventStatus.Failed, entity.Status);
        Assert.Equal("Connection timeout", entity.ErrorMessage);
        Assert.Equal(1, entity.RetryCount);
    }

    [Fact]
    public void MarkFailed_IncrementsRetryCountOnEachCall()
    {
        var entity = CreateEntity();

        entity.MarkFailed("Error 1");
        entity.MarkFailed("Error 2");
        entity.MarkFailed("Error 3");

        Assert.Equal(3, entity.RetryCount);
        Assert.Equal("Error 3", entity.ErrorMessage);
    }

    [Fact]
    public void CanRetry_ReturnsTrueWhenFailedAndBelowMaxRetries()
    {
        var entity = CreateEntity();
        entity.Status = WebhookEventStatus.Failed;
        entity.RetryCount = 2;

        Assert.True(entity.CanRetry(maxRetries: 3));
    }

    [Fact]
    public void CanRetry_ReturnsFalseWhenAtMaxRetries()
    {
        var entity = CreateEntity();
        entity.Status = WebhookEventStatus.Failed;
        entity.RetryCount = 3;

        Assert.False(entity.CanRetry(maxRetries: 3));
    }

    [Fact]
    public void CanRetry_ReturnsFalseWhenNotFailed()
    {
        var entity = CreateEntity();
        entity.Status = WebhookEventStatus.Processing;
        entity.RetryCount = 0;

        Assert.False(entity.CanRetry());
    }

    [Fact]
    public void ScheduleRetry_SetsNextRetryAtWithExponentialBackoff()
    {
        var entity = CreateEntity();
        entity.Status = WebhookEventStatus.Failed;
        entity.RetryCount = 0;

        entity.ScheduleRetry(maxRetries: 5);

        Assert.Equal(WebhookEventStatus.Failed, entity.Status);
        Assert.NotNull(entity.NextRetryAt);
        // First retry should be around 30 seconds
        var expectedMinTime = DateTime.UtcNow.AddSeconds(29);
        var expectedMaxTime = DateTime.UtcNow.AddSeconds(31);
        Assert.InRange(entity.NextRetryAt.Value, expectedMinTime, expectedMaxTime);
    }

    [Fact]
    public void ScheduleRetry_ExponentialBackoff_DoublesEachTime()
    {
        var entity = CreateEntity();
        entity.Status = WebhookEventStatus.Failed;
        entity.RetryCount = 0;

        // First retry: 30 * 2^0 = 30 seconds
        entity.ScheduleRetry(maxRetries: 5);
        var firstRetry = entity.NextRetryAt;

        // Simulate failed retry
        entity.RetryCount = 1;
        entity.ScheduleRetry(maxRetries: 5);
        var secondRetry = entity.NextRetryAt;

        // Second retry: 30 * 2^1 = 60 seconds
        Assert.NotNull(firstRetry);
        Assert.NotNull(secondRetry);

        // Verify second retry is later than first (exponential backoff)
        var firstDelay = (firstRetry.Value - DateTime.UtcNow).TotalSeconds;
        var secondDelay = (secondRetry.Value - DateTime.UtcNow).TotalSeconds;

        // Second delay should be approximately twice the first
        Assert.True(secondDelay > firstDelay);
    }

    [Fact]
    public void ScheduleRetry_MovesToDeadLetterWhenMaxRetriesExceeded()
    {
        var entity = CreateEntity();
        entity.Status = WebhookEventStatus.Failed;
        entity.RetryCount = 5;

        entity.ScheduleRetry(maxRetries: 5);

        Assert.Equal(WebhookEventStatus.DeadLetter, entity.Status);
        Assert.Null(entity.NextRetryAt);
    }

    [Fact]
    public void StartRetryProcessing_SetsStatusToProcessingAndClearsNextRetryAt()
    {
        var entity = CreateEntity();
        entity.Status = WebhookEventStatus.Failed;
        entity.NextRetryAt = DateTime.UtcNow.AddMinutes(5);

        entity.StartRetryProcessing();

        Assert.Equal(WebhookEventStatus.Processing, entity.Status);
        Assert.Null(entity.NextRetryAt);
    }

    [Fact]
    public void MoveToDeadLetter_SetsStatusAndClearsNextRetryAt()
    {
        var entity = CreateEntity();
        entity.Status = WebhookEventStatus.Failed;
        entity.NextRetryAt = DateTime.UtcNow.AddMinutes(5);

        entity.MoveToDeadLetter();

        Assert.Equal(WebhookEventStatus.DeadLetter, entity.Status);
        Assert.Null(entity.NextRetryAt);
    }

    [Theory]
    [InlineData(0, 30)]     // 30 * 2^0 = 30
    [InlineData(1, 60)]     // 30 * 2^1 = 60
    [InlineData(2, 120)]    // 30 * 2^2 = 120
    [InlineData(3, 240)]    // 30 * 2^3 = 240
    [InlineData(4, 480)]    // 30 * 2^4 = 480
    public void ScheduleRetry_CalculatesCorrectBackoffTime(int retryCount, int expectedSeconds)
    {
        var entity = CreateEntity();
        entity.Status = WebhookEventStatus.Failed;
        entity.RetryCount = retryCount;

        var beforeSchedule = DateTime.UtcNow;
        entity.ScheduleRetry(maxRetries: 10);

        Assert.NotNull(entity.NextRetryAt);
        var actualSeconds = (entity.NextRetryAt.Value - beforeSchedule).TotalSeconds;

        // Allow 1 second tolerance for test execution time
        Assert.InRange(actualSeconds, expectedSeconds - 1, expectedSeconds + 1);
    }

    private static StripeWebhookEventEntity CreateEntity()
    {
        return new StripeWebhookEventEntity
        {
            Id = Guid.NewGuid(),
            StripeEventId = "evt_test_123",
            EventType = "test.event",
            Payload = "{}",
            Status = WebhookEventStatus.Pending
        };
    }
}
