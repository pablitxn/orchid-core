using FluentAssertions;
using Infrastructure.Persistence.Seeders;

namespace Infrastructure.Tests.Seeders;

public class SeedingStatusServiceTests
{
    [Fact]
    public void IsSeedingComplete_Should_BeFalse_Initially()
    {
        // Arrange
        var service = new SeedingStatusService();

        // Assert
        service.IsSeedingComplete.Should().BeFalse();
    }

    [Fact]
    public void SeedingError_Should_BeNull_Initially()
    {
        // Arrange
        var service = new SeedingStatusService();

        // Assert
        service.SeedingError.Should().BeNull();
    }

    [Fact]
    public void MarkComplete_Should_SetIsSeedingCompleteToTrue()
    {
        // Arrange
        var service = new SeedingStatusService();

        // Act
        service.MarkComplete();

        // Assert
        service.IsSeedingComplete.Should().BeTrue();
        service.SeedingError.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_Should_SetError()
    {
        // Arrange
        var service = new SeedingStatusService();
        var error = new InvalidOperationException("Seeding failed");

        // Act
        service.MarkFailed(error);

        // Assert
        service.IsSeedingComplete.Should().BeTrue();
        service.SeedingError.Should().BeSameAs(error);
    }

    [Fact]
    public async Task WaitForSeedingAsync_Should_ReturnImmediately_When_AlreadyComplete()
    {
        // Arrange
        var service = new SeedingStatusService();
        service.MarkComplete();

        // Act
        var task = service.WaitForSeedingAsync();

        // Assert
        task.IsCompleted.Should().BeTrue();
        await task; // Should not throw
    }

    [Fact]
    public async Task WaitForSeedingAsync_Should_WaitUntilComplete()
    {
        // Arrange
        var service = new SeedingStatusService();
        var completed = false;

        // Act
        var waitTask = Task.Run(async () =>
        {
            await service.WaitForSeedingAsync();
            completed = true;
        });

        // Give the wait task time to start
        await Task.Delay(50);
        completed.Should().BeFalse();

        // Complete the seeding
        service.MarkComplete();
        await waitTask;

        // Assert
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForSeedingAsync_Should_Throw_When_SeedingFailed()
    {
        // Arrange
        var service = new SeedingStatusService();
        var error = new InvalidOperationException("Seeding failed");
        service.MarkFailed(error);

        // Act
        var act = () => service.WaitForSeedingAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Seeding failed");
    }

    [Fact]
    public async Task WaitForSeedingAsync_Should_SupportCancellation()
    {
        // Arrange
        var service = new SeedingStatusService();
        var cts = new CancellationTokenSource();

        // Act
        var waitTask = service.WaitForSeedingAsync(cts.Token);
        cts.Cancel();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => waitTask);
    }

    [Fact]
    public async Task WaitForSeedingAsync_Should_Complete_When_MarkedAfterWaiting()
    {
        // Arrange
        var service = new SeedingStatusService();

        // Start waiting first
        var waitTask = service.WaitForSeedingAsync();

        // Small delay to ensure wait is registered
        await Task.Delay(10);

        // Mark complete
        service.MarkComplete();

        // Act & Assert
        await waitTask; // Should complete without throwing
        service.IsSeedingComplete.Should().BeTrue();
    }

    [Fact]
    public void MarkComplete_Should_BeIdempotent()
    {
        // Arrange
        var service = new SeedingStatusService();

        // Act
        service.MarkComplete();
        service.MarkComplete();
        service.MarkComplete();

        // Assert
        service.IsSeedingComplete.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleWaiters_Should_AllComplete()
    {
        // Arrange
        var service = new SeedingStatusService();
        var completionCount = 0;

        // Act - Start multiple waiters
        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            await service.WaitForSeedingAsync();
            Interlocked.Increment(ref completionCount);
        })).ToList();

        // Give tasks time to start waiting
        await Task.Delay(50);

        // Complete seeding
        service.MarkComplete();

        // Wait for all tasks
        await Task.WhenAll(tasks);

        // Assert
        completionCount.Should().Be(5);
    }

    [Fact]
    public async Task WaitForSeedingAsync_Should_NotBlock_AfterCompletion()
    {
        // Arrange
        var service = new SeedingStatusService();
        service.MarkComplete();

        // Act - Multiple calls after completion should return immediately
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.WaitForSeedingAsync())
            .ToList();

        // Assert
        await Task.WhenAll(tasks);
        tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());
    }

    [Fact]
    public void MarkFailed_Should_PreserveOriginalException()
    {
        // Arrange
        var service = new SeedingStatusService();
        var innerException = new InvalidOperationException("Inner error");
        var error = new AggregateException("Outer error", innerException);

        // Act
        service.MarkFailed(error);

        // Assert
        service.SeedingError.Should().BeSameAs(error);
        service.SeedingError.Should().BeOfType<AggregateException>();
    }

    [Fact]
    public async Task CancellationAfterCompletion_Should_NotAffectResult()
    {
        // Arrange
        var service = new SeedingStatusService();
        var cts = new CancellationTokenSource();
        service.MarkComplete();

        // Act - Cancel after already complete
        cts.Cancel();
        var act = () => service.WaitForSeedingAsync(cts.Token);

        // Assert - Should still complete successfully since already marked complete
        await act.Should().NotThrowAsync();
    }
}
