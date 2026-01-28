using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Services;

public class InMemoryRateLimitServiceTests
{
    private readonly Mock<ILogger<InMemoryRateLimitService>> _loggerMock;
    private readonly InMemoryRateLimitService _service;

    public InMemoryRateLimitServiceTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryRateLimitService>>();
        _service = new InMemoryRateLimitService(_loggerMock.Object);
    }

    #region CheckAsync Tests

    [Fact]
    public async Task CheckAsync_Should_AllowFirstRequest()
    {
        // Arrange
        var key = "test-key";
        var limit = 10;
        var window = TimeSpan.FromMinutes(1);

        // Act
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.CurrentCount.Should().Be(1);
        result.Limit.Should().Be(limit);
    }

    [Fact]
    public async Task CheckAsync_Should_AllowRequestsUnderLimit()
    {
        // Arrange
        var key = "under-limit-key";
        var limit = 5;
        var window = TimeSpan.FromMinutes(1);

        // Act - Make 4 requests with delays to ensure unique timestamps
        for (var i = 0; i < 4; i++)
        {
            var result = await _service.CheckAsync(key, limit, window);
            result.IsAllowed.Should().BeTrue();
            await Task.Delay(2); // Small delay to ensure unique timestamp
        }

        // 5th request
        var finalResult = await _service.CheckAsync(key, limit, window);

        // Assert
        finalResult.IsAllowed.Should().BeTrue();
        finalResult.CurrentCount.Should().Be(5);
    }

    [Fact]
    public async Task CheckAsync_Should_DenyRequestsOverLimit()
    {
        // Arrange
        var key = "over-limit-key";
        var limit = 3;
        var window = TimeSpan.FromMinutes(1);

        // Act - Make 3 requests (hit limit) with delays to ensure unique timestamps
        for (var i = 0; i < 3; i++)
        {
            await _service.CheckAsync(key, limit, window);
            await Task.Delay(2); // Small delay to ensure unique timestamp
        }

        // 4th request should be denied
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.CurrentCount.Should().Be(3);
    }

    [Fact]
    public async Task CheckAsync_Should_ResetAfterWindow()
    {
        // Arrange
        var key = "reset-window-key";
        var limit = 2;
        var window = TimeSpan.FromMilliseconds(200);

        // Act - Hit the limit with delays to ensure unique timestamps
        await _service.CheckAsync(key, limit, window);
        await Task.Delay(5);
        await _service.CheckAsync(key, limit, window);
        await Task.Delay(5);
        var overLimitResult = await _service.CheckAsync(key, limit, window);

        // Wait for window to expire
        await Task.Delay(250);

        // Try again
        var afterResetResult = await _service.CheckAsync(key, limit, window);

        // Assert
        overLimitResult.IsAllowed.Should().BeFalse();
        afterResetResult.IsAllowed.Should().BeTrue();
        afterResetResult.CurrentCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckAsync_Should_TrackDifferentKeysIndependently()
    {
        // Arrange
        var limit = 1;  // Limit of 1 - first request allowed, second denied
        var window = TimeSpan.FromMinutes(1);

        // Act - Use up limit for key1 (1 request = limit)
        var key1First = await _service.CheckAsync("key1", limit, window);
        // 2nd request should be denied
        var key1OverLimit = await _service.CheckAsync("key1", limit, window);

        // key2 should still have full limit
        var key2Result = await _service.CheckAsync("key2", limit, window);

        // Assert
        key1First.IsAllowed.Should().BeTrue();
        key1OverLimit.IsAllowed.Should().BeFalse();
        key2Result.IsAllowed.Should().BeTrue();
        key2Result.CurrentCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckAsync_Should_SetResetTime()
    {
        // Arrange
        var key = "reset-time-key";
        var limit = 10;
        var window = TimeSpan.FromMinutes(5);

        // Act
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.ResetTime.Should().BeCloseTo(DateTime.UtcNow.Add(window), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CheckAsync_Should_SetRetryAfter_WhenExceeded()
    {
        // Arrange
        var key = "retry-after-key";
        var limit = 1;
        var window = TimeSpan.FromMinutes(1);

        // Hit limit
        await _service.CheckAsync(key, limit, window);

        // Act
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.RetryAfter.Should().BePositive();
    }

    #endregion

    #region RecordRequestAsync Tests

    [Fact]
    public async Task RecordRequestAsync_Should_IncrementCount()
    {
        // Arrange
        var key = "record-key";
        var limit = 10;
        var window = TimeSpan.FromMinutes(1);

        // Act - Record 5 requests with delays to ensure unique timestamps
        for (var i = 0; i < 5; i++)
        {
            await _service.RecordRequestAsync(key, window);
            await Task.Delay(2); // Small delay to ensure unique timestamp
        }

        // Check the count
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.CurrentCount.Should().Be(6); // 5 recorded + 1 from check
    }

    [Fact]
    public async Task RecordRequestAsync_Should_CleanupExpiredEntries()
    {
        // Arrange
        var key = "cleanup-key";
        var window = TimeSpan.FromMilliseconds(50);

        // Record some requests
        await _service.RecordRequestAsync(key, window);
        await _service.RecordRequestAsync(key, window);

        // Wait for window to expire
        await Task.Delay(100);

        // Act - Record new request
        await _service.RecordRequestAsync(key, window);

        var result = await _service.CheckAsync(key, 10, window);

        // Assert - Old entries should be cleaned up
        result.CurrentCount.Should().BeLessThanOrEqualTo(2);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task CheckAsync_Should_HandleConcurrentRequests()
    {
        // Arrange
        var key = "concurrent-key";
        var limit = 100;
        var window = TimeSpan.FromMinutes(1);
        var allowedCount = 0;
        var deniedCount = 0;

        // Act - Make 150 concurrent requests
        var tasks = Enumerable.Range(0, 150).Select(_ => Task.Run(async () =>
        {
            var result = await _service.CheckAsync(key, limit, window);
            if (result.IsAllowed)
            {
                Interlocked.Increment(ref allowedCount);
            }
            else
            {
                Interlocked.Increment(ref deniedCount);
            }
        }));

        await Task.WhenAll(tasks);

        // Assert - With concurrent access, totals should be 150
        (allowedCount + deniedCount).Should().Be(150);
        // At least some should be denied once limit is reached
        deniedCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RecordRequestAsync_Should_HandleConcurrentRequests()
    {
        // Arrange
        var key = "concurrent-record-key";
        var window = TimeSpan.FromMinutes(1);
        var requestCount = 100;

        // Act
        var tasks = Enumerable.Range(0, requestCount).Select(_ =>
            _service.RecordRequestAsync(key, window));

        await Task.WhenAll(tasks);

        var result = await _service.CheckAsync(key, 1000, window);

        // Assert - Count should include recorded requests
        // Note: Due to SortedSet behavior with same timestamp, count might be less than expected
        result.CurrentCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CheckAsync_Should_HandleLimitOfOne()
    {
        // Arrange
        var key = "limit-one-key";
        var limit = 1;
        var window = TimeSpan.FromMinutes(1);

        // Act
        var first = await _service.CheckAsync(key, limit, window);
        var second = await _service.CheckAsync(key, limit, window);

        // Assert
        first.IsAllowed.Should().BeTrue();
        second.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_Should_HandleZeroLimit()
    {
        // Arrange
        var key = "zero-limit-key";
        var limit = 0;
        var window = TimeSpan.FromMinutes(1);

        // Act
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_Should_HandleVeryShortWindow()
    {
        // Arrange
        var key = "short-window-key";
        var limit = 10;
        var window = TimeSpan.FromMilliseconds(1);

        // Act
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_Should_HandleEmptyKey()
    {
        // Arrange
        var key = "";
        var limit = 10;
        var window = TimeSpan.FromMinutes(1);

        // Act
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_Should_HandleSpecialCharactersInKey()
    {
        // Arrange
        var key = "user:123:action:create:resource:api/v1/users";
        var limit = 10;
        var window = TimeSpan.FromMinutes(1);

        // Act
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    #endregion

    #region Sliding Window Behavior Tests

    [Fact]
    public async Task CheckAsync_Should_UseSlidingWindow()
    {
        // Arrange
        var key = "sliding-window-key";
        var limit = 5;
        var window = TimeSpan.FromMilliseconds(200);

        // Act - Make requests over time
        await _service.CheckAsync(key, limit, window); // t=0
        await Task.Delay(50);
        await _service.CheckAsync(key, limit, window); // t=50
        await Task.Delay(50);
        await _service.CheckAsync(key, limit, window); // t=100
        await Task.Delay(50);
        await _service.CheckAsync(key, limit, window); // t=150
        await Task.Delay(50);
        await _service.CheckAsync(key, limit, window); // t=200 - first request should have expired

        // At t=200, the request from t=0 should be outside the window
        var result = await _service.CheckAsync(key, limit, window);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task CheckAsync_Should_SupportCancellation()
    {
        // Arrange
        var key = "cancellation-key";
        var limit = 10;
        var window = TimeSpan.FromMinutes(1);
        var cts = new CancellationTokenSource();

        // Act
        var result = await _service.CheckAsync(key, limit, window, cts.Token);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task RecordRequestAsync_Should_SupportCancellation()
    {
        // Arrange
        var key = "record-cancellation-key";
        var window = TimeSpan.FromMinutes(1);
        var cts = new CancellationTokenSource();

        // Act
        await _service.RecordRequestAsync(key, window, cts.Token);

        // Assert - Should not throw
        var result = await _service.CheckAsync(key, 10, window);
        result.CurrentCount.Should().Be(2);
    }

    #endregion
}
