using Application.Interfaces;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchid.Infrastructure.Ai.AgentFramework;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class ThreadCleanupBackgroundServiceTests
{
    private readonly Mock<IAgentThreadManager> _threadManagerMock;
    private readonly Mock<ILogger<ThreadCleanupBackgroundService>> _loggerMock;
    private readonly ThreadManagerSettings _settings;

    public ThreadCleanupBackgroundServiceTests()
    {
        _threadManagerMock = new Mock<IAgentThreadManager>();
        _loggerMock = new Mock<ILogger<ThreadCleanupBackgroundService>>();
        _settings = new ThreadManagerSettings
        {
            ThreadIdleTimeoutMinutes = 60,
            CleanupIntervalSeconds = 1, // Short interval for testing
            PersistToRedisOnCleanup = true
        };
    }

    private ThreadCleanupBackgroundService CreateService()
    {
        return new ThreadCleanupBackgroundService(
            _threadManagerMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ThreadManagerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ThreadCleanupBackgroundService(
                null!,
                Options.Create(_settings),
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_SettingsIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ThreadCleanupBackgroundService(
                _threadManagerMock.Object,
                null!,
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ThreadCleanupBackgroundService(
                _threadManagerMock.Object,
                Options.Create(_settings),
                null!));
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_Should_CleanupExpiredThreads()
    {
        // Arrange
        var expiredSessions = new List<string> { "session-1", "session-2" };
        _threadManagerMock
            .Setup(m => m.GetExpiredSessionIds(It.IsAny<TimeSpan>()))
            .Returns(expiredSessions.AsReadOnly());

        _threadManagerMock
            .Setup(m => m.CleanupThreadAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _threadManagerMock
            .Setup(m => m.GetActiveThreadCount())
            .Returns(0);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act - Start the service and let it run one cycle
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100); // Give it time to run one cleanup cycle
        cts.Cancel();

        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _threadManagerMock.Verify(
            m => m.GetExpiredSessionIds(TimeSpan.FromMinutes(_settings.ThreadIdleTimeoutMinutes)),
            Times.AtLeastOnce);

        _threadManagerMock.Verify(
            m => m.CleanupThreadAsync("session-1", true, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _threadManagerMock.Verify(
            m => m.CleanupThreadAsync("session-2", true, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotCleanup_When_NoExpiredThreads()
    {
        // Arrange
        _threadManagerMock
            .Setup(m => m.GetExpiredSessionIds(It.IsAny<TimeSpan>()))
            .Returns(new List<string>().AsReadOnly());

        _threadManagerMock
            .Setup(m => m.GetActiveThreadCount())
            .Returns(5);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _threadManagerMock.Verify(
            m => m.CleanupThreadAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ContinueOnCleanupError()
    {
        // Arrange
        var expiredSessions = new List<string> { "session-error", "session-success" };
        _threadManagerMock
            .Setup(m => m.GetExpiredSessionIds(It.IsAny<TimeSpan>()))
            .Returns(expiredSessions.AsReadOnly());

        _threadManagerMock
            .Setup(m => m.CleanupThreadAsync("session-error", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated error"));

        _threadManagerMock
            .Setup(m => m.CleanupThreadAsync("session-success", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _threadManagerMock
            .Setup(m => m.GetActiveThreadCount())
            .Returns(0);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Both sessions should be attempted despite first one failing
        _threadManagerMock.Verify(
            m => m.CleanupThreadAsync("session-error", It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _threadManagerMock.Verify(
            m => m.CleanupThreadAsync("session-success", It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_Should_UseConfiguredSettings()
    {
        // Arrange
        _settings.ThreadIdleTimeoutMinutes = 30;
        _settings.PersistToRedisOnCleanup = false;

        var expiredSessions = new List<string> { "session-1" };
        _threadManagerMock
            .Setup(m => m.GetExpiredSessionIds(TimeSpan.FromMinutes(30)))
            .Returns(expiredSessions.AsReadOnly());

        _threadManagerMock
            .Setup(m => m.CleanupThreadAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _threadManagerMock
            .Setup(m => m.GetActiveThreadCount())
            .Returns(0);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _threadManagerMock.Verify(
            m => m.GetExpiredSessionIds(TimeSpan.FromMinutes(30)),
            Times.AtLeastOnce);

        _threadManagerMock.Verify(
            m => m.CleanupThreadAsync("session-1", false, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    #endregion
}
