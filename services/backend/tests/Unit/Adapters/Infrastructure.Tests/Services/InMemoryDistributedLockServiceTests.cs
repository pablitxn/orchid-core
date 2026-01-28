using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Services;

public class InMemoryDistributedLockServiceTests
{
    private readonly Mock<ILogger<InMemoryDistributedLockService>> _loggerMock;
    private readonly InMemoryDistributedLockService _service;

    public InMemoryDistributedLockServiceTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryDistributedLockService>>();
        _service = new InMemoryDistributedLockService(_loggerMock.Object);
    }

    #region AcquireAsync Tests

    [Fact]
    public async Task AcquireAsync_Should_ReturnLock_When_KeyNotHeld()
    {
        // Arrange
        var key = "test-lock";
        var expiry = TimeSpan.FromMinutes(1);

        // Act
        var lockHandle = await _service.AcquireAsync(key, expiry);

        // Assert
        lockHandle.Should().NotBeNull();
        lockHandle!.Key.Should().Be(key);
        lockHandle.IsHeld.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_Should_ReturnNull_When_KeyAlreadyHeld()
    {
        // Arrange
        var key = "already-held-lock";
        var expiry = TimeSpan.FromMinutes(1);

        // Acquire first lock
        var firstLock = await _service.AcquireAsync(key, expiry);

        // Act
        var secondLock = await _service.AcquireAsync(key, expiry);

        // Assert
        firstLock.Should().NotBeNull();
        secondLock.Should().BeNull();
    }

    [Fact]
    public async Task AcquireAsync_Should_ReturnLock_After_PreviousLockExpired()
    {
        // Arrange
        var key = "expiring-lock";
        var shortExpiry = TimeSpan.FromMilliseconds(50);

        // Acquire first lock with short expiry
        var firstLock = await _service.AcquireAsync(key, shortExpiry);
        firstLock.Should().NotBeNull();

        // Wait for expiry
        await Task.Delay(100);

        // Act
        var secondLock = await _service.AcquireAsync(key, TimeSpan.FromMinutes(1));

        // Assert
        secondLock.Should().NotBeNull();
        secondLock!.Key.Should().Be(key);
    }

    [Fact]
    public async Task AcquireAsync_Should_ReturnLock_After_PreviousLockReleased()
    {
        // Arrange
        var key = "released-lock";
        var expiry = TimeSpan.FromMinutes(1);

        var firstLock = await _service.AcquireAsync(key, expiry);
        await firstLock!.DisposeAsync();

        // Act
        var secondLock = await _service.AcquireAsync(key, expiry);

        // Assert
        secondLock.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireAsync_Should_HandleMultipleDifferentKeys()
    {
        // Arrange & Act
        var lock1 = await _service.AcquireAsync("key1", TimeSpan.FromMinutes(1));
        var lock2 = await _service.AcquireAsync("key2", TimeSpan.FromMinutes(1));
        var lock3 = await _service.AcquireAsync("key3", TimeSpan.FromMinutes(1));

        // Assert
        lock1.Should().NotBeNull();
        lock2.Should().NotBeNull();
        lock3.Should().NotBeNull();
    }

    #endregion

    #region WaitAsync Tests

    [Fact]
    public async Task WaitAsync_Should_ReturnLock_Immediately_When_KeyNotHeld()
    {
        // Arrange
        var key = "wait-immediate";
        var expiry = TimeSpan.FromMinutes(1);
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var lockHandle = await _service.WaitAsync(key, expiry, timeout);

        // Assert
        lockHandle.Should().NotBeNull();
        lockHandle.Key.Should().Be(key);
    }

    [Fact]
    public async Task WaitAsync_Should_WaitForLockRelease()
    {
        // Arrange
        var key = "wait-for-release";
        var expiry = TimeSpan.FromMinutes(1);
        var timeout = TimeSpan.FromSeconds(5);

        var firstLock = await _service.AcquireAsync(key, expiry);

        // Release lock after delay
        var releaseTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            await firstLock!.DisposeAsync();
        });

        // Act
        var secondLock = await _service.WaitAsync(key, expiry, timeout);

        // Assert
        secondLock.Should().NotBeNull();
        await releaseTask;
    }

    [Fact]
    public async Task WaitAsync_Should_ThrowTimeout_When_LockNotAvailable()
    {
        // Arrange
        var key = "wait-timeout";
        var expiry = TimeSpan.FromMinutes(1);
        var timeout = TimeSpan.FromMilliseconds(100);

        var firstLock = await _service.AcquireAsync(key, expiry);

        // Act
        var act = () => _service.WaitAsync(key, expiry, timeout);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage($"*{key}*");
    }

    [Fact]
    public async Task WaitAsync_Should_SupportCancellation()
    {
        // Arrange
        var key = "wait-cancellation";
        var expiry = TimeSpan.FromMinutes(1);
        var timeout = TimeSpan.FromSeconds(5);
        var cts = new CancellationTokenSource();

        var firstLock = await _service.AcquireAsync(key, expiry);

        // Cancel after short delay
        cts.CancelAfter(50);

        // Act
        var act = () => _service.WaitAsync(key, expiry, timeout, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Lock Extension Tests

    [Fact]
    public async Task ExtendAsync_Should_ExtendLockExpiry()
    {
        // Arrange
        var key = "extend-lock";
        var initialExpiry = TimeSpan.FromMilliseconds(100);
        var extension = TimeSpan.FromMinutes(1);

        var lockHandle = await _service.AcquireAsync(key, initialExpiry);

        // Act - Extend before expiry
        await lockHandle!.ExtendAsync(extension);

        // Wait past initial expiry
        await Task.Delay(150);

        // Assert - Lock should still be held
        var anotherLock = await _service.AcquireAsync(key, TimeSpan.FromMinutes(1));
        anotherLock.Should().BeNull();
    }

    [Fact]
    public async Task ExtendAsync_Should_ThrowException_When_LockReleased()
    {
        // Arrange
        var key = "extend-released";
        var expiry = TimeSpan.FromMinutes(1);

        var lockHandle = await _service.AcquireAsync(key, expiry);
        await lockHandle!.DisposeAsync();

        // Act
        var act = () => lockHandle.ExtendAsync(TimeSpan.FromMinutes(1));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no longer held*");
    }

    [Fact]
    public async Task ExtendAsync_Should_ThrowException_When_LockLost()
    {
        // Arrange
        var key = "extend-lost";
        var shortExpiry = TimeSpan.FromMilliseconds(50);

        var lockHandle = await _service.AcquireAsync(key, shortExpiry);

        // Wait for expiry
        await Task.Delay(100);

        // Someone else takes the lock
        var otherLock = await _service.AcquireAsync(key, TimeSpan.FromMinutes(1));
        otherLock.Should().NotBeNull();

        // Act
        var act = () => lockHandle!.ExtendAsync(TimeSpan.FromMinutes(1));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*lost*");
    }

    #endregion

    #region Lock Disposal Tests

    [Fact]
    public async Task DisposeAsync_Should_ReleaseLock()
    {
        // Arrange
        var key = "dispose-release";
        var expiry = TimeSpan.FromMinutes(1);

        var lockHandle = await _service.AcquireAsync(key, expiry);

        // Act
        await lockHandle!.DisposeAsync();

        // Assert
        lockHandle.IsHeld.Should().BeFalse();
        var newLock = await _service.AcquireAsync(key, expiry);
        newLock.Should().NotBeNull();
    }

    [Fact]
    public async Task DisposeAsync_Should_BeIdempotent()
    {
        // Arrange
        var key = "dispose-idempotent";
        var expiry = TimeSpan.FromMinutes(1);

        var lockHandle = await _service.AcquireAsync(key, expiry);

        // Act
        await lockHandle!.DisposeAsync();
        await lockHandle.DisposeAsync();
        await lockHandle.DisposeAsync();

        // Assert - Should not throw
        lockHandle.IsHeld.Should().BeFalse();
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentAcquire_Should_OnlyAllowOneLock()
    {
        // Arrange
        var key = "concurrent-lock";
        var expiry = TimeSpan.FromMinutes(1);
        var acquiredCount = 0;

        // Act
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            var lockHandle = await _service.AcquireAsync(key, expiry);
            if (lockHandle != null)
            {
                Interlocked.Increment(ref acquiredCount);
            }
        }));

        await Task.WhenAll(tasks);

        // Assert - Only one should succeed
        acquiredCount.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentAcquireDifferentKeys_Should_AllSucceed()
    {
        // Arrange
        var expiry = TimeSpan.FromMinutes(1);
        var acquiredCount = 0;

        // Act
        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            var lockHandle = await _service.AcquireAsync($"key-{i}", expiry);
            if (lockHandle != null)
            {
                Interlocked.Increment(ref acquiredCount);
            }
        }));

        await Task.WhenAll(tasks);

        // Assert - All should succeed
        acquiredCount.Should().Be(10);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task AcquireAsync_Should_HandleEmptyKey()
    {
        // Arrange
        var key = "";
        var expiry = TimeSpan.FromMinutes(1);

        // Act
        var lockHandle = await _service.AcquireAsync(key, expiry);

        // Assert
        lockHandle.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireAsync_Should_HandleVeryShortExpiry()
    {
        // Arrange
        var key = "short-expiry";
        var expiry = TimeSpan.FromMilliseconds(1);

        // Act
        var lockHandle = await _service.AcquireAsync(key, expiry);

        // Assert
        lockHandle.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireAsync_Should_HandleLongKey()
    {
        // Arrange
        var key = new string('x', 10000);
        var expiry = TimeSpan.FromMinutes(1);

        // Act
        var lockHandle = await _service.AcquireAsync(key, expiry);

        // Assert
        lockHandle.Should().NotBeNull();
    }

    #endregion
}
