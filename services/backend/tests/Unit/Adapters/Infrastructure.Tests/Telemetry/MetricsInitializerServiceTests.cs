using Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Telemetry;

public class MetricsInitializerServiceTests
{
    private readonly Mock<ILogger<MetricsInitializerService>> _loggerMock;
    private readonly MetricsInitializerService _service;

    public MetricsInitializerServiceTests()
    {
        _loggerMock = new Mock<ILogger<MetricsInitializerService>>();
        _service = new MetricsInitializerService(_loggerMock.Object);
    }

    [Fact]
    public async Task StartAsync_InitializesMetricsWithoutErrors()
    {
        // Act
        var exception = await Record.ExceptionAsync(() =>
            _service.StartAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task StartAsync_LogsInitializationMessages()
    {
        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert - verify logging was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing Prometheus metrics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Prometheus metrics initialized successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_CanBeCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Should complete immediately since initialization is synchronous
        var task = _service.StartAsync(cts.Token);

        // Assert - Task completes (cancellation doesn't interrupt sync code)
        await task;
    }

    [Fact]
    public async Task StopAsync_CompletesImmediately()
    {
        // Act
        var exception = await Record.ExceptionAsync(() =>
            _service.StopAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task StartAsync_CanBeCalledMultipleTimes()
    {
        // Act - Should handle multiple calls gracefully (idempotent)
        var exception = await Record.ExceptionAsync(async () =>
        {
            await _service.StartAsync(CancellationToken.None);
            await _service.StartAsync(CancellationToken.None);
        });

        // Assert
        Assert.Null(exception);
    }
}
