using Application.Interfaces;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class AgentThreadManagerTests
{
    private readonly Mock<ICacheStore> _cacheMock;
    private readonly Mock<ILogger<AgentThreadManager>> _loggerMock;
    private readonly AgentThreadManager _manager;

    public AgentThreadManagerTests()
    {
        _cacheMock = new Mock<ICacheStore>();
        _loggerMock = new Mock<ILogger<AgentThreadManager>>();
        _manager = new AgentThreadManager(_cacheMock.Object, _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_CacheIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentThreadManager(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentThreadManager(_cacheMock.Object, null!));
    }

    #endregion

    #region GetOrCreateThreadAsync Tests

    [Fact]
    public async Task GetOrCreateThreadAsync_Should_CreateNewThread_When_NoExistingThread()
    {
        // Arrange
        var sessionId = "test-session-1";
        var agentId = Guid.NewGuid();

        _cacheMock
            .Setup(c => c.GetStringAsync($"chat_history:{sessionId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _manager.GetOrCreateThreadAsync(sessionId, agentId, CancellationToken.None);

        // Assert
        result.SessionId.Should().Be(sessionId);
        result.MessageCount.Should().Be(0);
        result.WasMigrated.Should().BeFalse();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetOrCreateThreadAsync_Should_ReturnExistingThread_When_ThreadExists()
    {
        // Arrange
        var sessionId = "test-session-2";
        var agentId = Guid.NewGuid();

        _cacheMock
            .Setup(c => c.GetStringAsync($"chat_history:{sessionId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Create the thread first
        var firstResult = await _manager.GetOrCreateThreadAsync(sessionId, agentId, CancellationToken.None);

        // Add a message to verify it's the same thread
        await _manager.AddMessageAsync(sessionId, "user", "Hello", CancellationToken.None);

        // Act - get the same thread again
        var secondResult = await _manager.GetOrCreateThreadAsync(sessionId, agentId, CancellationToken.None);

        // Assert
        secondResult.SessionId.Should().Be(sessionId);
        secondResult.MessageCount.Should().Be(1);
        secondResult.CreatedAt.Should().Be(firstResult.CreatedAt);
    }

    [Fact]
    public async Task GetOrCreateThreadAsync_Should_MigrateFromRedis_When_RedisHistoryExists()
    {
        // Arrange
        var sessionId = "test-session-migrate";
        var agentId = Guid.NewGuid();

        var redisHistory = new List<ChatMessage>
        {
            new("user", "Hello from Redis"),
            new("assistant", "Hi there!")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(redisHistory,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        _cacheMock
            .Setup(c => c.GetStringAsync($"chat_history:{sessionId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await _manager.GetOrCreateThreadAsync(sessionId, agentId, CancellationToken.None);

        // Assert
        result.SessionId.Should().Be(sessionId);
        result.MessageCount.Should().Be(2);
        result.WasMigrated.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrCreateThreadAsync_Should_ThrowArgumentException_When_SessionIdIsEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.GetOrCreateThreadAsync("", Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetOrCreateThreadAsync_Should_ThrowArgumentException_When_SessionIdIsWhitespace()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.GetOrCreateThreadAsync("   ", Guid.NewGuid(), CancellationToken.None));
    }

    #endregion

    #region MigrateFromRedisIfNeededAsync Tests

    [Fact]
    public async Task MigrateFromRedisIfNeededAsync_Should_ReturnFalse_When_NoRedisHistory()
    {
        // Arrange
        var sessionId = "test-session-no-history";

        _cacheMock
            .Setup(c => c.GetStringAsync($"chat_history:{sessionId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _manager.MigrateFromRedisIfNeededAsync(sessionId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MigrateFromRedisIfNeededAsync_Should_ReturnTrue_When_RedisHistoryExists()
    {
        // Arrange
        var sessionId = "test-session-with-history";

        var redisHistory = new List<ChatMessage>
        {
            new("user", "Message 1"),
            new("assistant", "Response 1")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(redisHistory,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        _cacheMock
            .Setup(c => c.GetStringAsync($"chat_history:{sessionId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await _manager.MigrateFromRedisIfNeededAsync(sessionId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        // Verify messages were migrated
        var history = await _manager.GetHistoryAsync(sessionId, CancellationToken.None);
        history.Count.Should().Be(2);
    }

    [Fact]
    public async Task MigrateFromRedisIfNeededAsync_Should_BeIdempotent()
    {
        // Arrange
        var sessionId = "test-session-idempotent";

        var redisHistory = new List<ChatMessage>
        {
            new("user", "Original message")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(redisHistory,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        _cacheMock
            .Setup(c => c.GetStringAsync($"chat_history:{sessionId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act - call migration twice
        var firstResult = await _manager.MigrateFromRedisIfNeededAsync(sessionId, CancellationToken.None);
        var secondResult = await _manager.MigrateFromRedisIfNeededAsync(sessionId, CancellationToken.None);

        // Assert
        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse(); // Second call should not migrate again

        // Verify no duplicate messages
        var history = await _manager.GetHistoryAsync(sessionId, CancellationToken.None);
        history.Count.Should().Be(1);
    }

    [Fact]
    public async Task MigrateFromRedisIfNeededAsync_Should_OnlyMigrateStandardRoles()
    {
        // Arrange
        var sessionId = "test-session-roles";

        var redisHistory = new List<ChatMessage>
        {
            new("user", "User message"),
            new("assistant", "Assistant message"),
            new("system", "System message"),
            new("activity", "Activity message") // Should be excluded
        };

        var json = System.Text.Json.JsonSerializer.Serialize(redisHistory,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        _cacheMock
            .Setup(c => c.GetStringAsync($"chat_history:{sessionId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        await _manager.MigrateFromRedisIfNeededAsync(sessionId, CancellationToken.None);

        // Assert
        var history = await _manager.GetHistoryAsync(sessionId, CancellationToken.None);
        history.Count.Should().Be(3); // Only user, assistant, system
        history.Should().NotContain(m => m.Role == "activity");
    }

    #endregion

    #region AddMessageAsync Tests

    [Fact]
    public async Task AddMessageAsync_Should_AddMessageToThread()
    {
        // Arrange
        var sessionId = "test-session-add";

        // Act
        await _manager.AddMessageAsync(sessionId, "user", "Hello", CancellationToken.None);

        // Assert
        var history = await _manager.GetHistoryAsync(sessionId, CancellationToken.None);
        history.Count.Should().Be(1);
        history[0].Role.Should().Be("user");
        history[0].Content.Should().Be("Hello");
    }

    [Fact]
    public async Task AddMessageAsync_Should_AddMultipleMessages()
    {
        // Arrange
        var sessionId = "test-session-multi";

        // Act
        await _manager.AddMessageAsync(sessionId, "user", "Question 1", CancellationToken.None);
        await _manager.AddMessageAsync(sessionId, "assistant", "Answer 1", CancellationToken.None);
        await _manager.AddMessageAsync(sessionId, "user", "Question 2", CancellationToken.None);

        // Assert
        var history = await _manager.GetHistoryAsync(sessionId, CancellationToken.None);
        history.Count.Should().Be(3);
    }

    [Fact]
    public async Task AddMessageAsync_Should_ThrowArgumentException_When_SessionIdIsEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.AddMessageAsync("", "user", "Hello", CancellationToken.None));
    }

    [Fact]
    public async Task AddMessageAsync_Should_ThrowArgumentException_When_RoleIsEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.AddMessageAsync("session", "", "Hello", CancellationToken.None));
    }

    #endregion

    #region GetHistoryAsync Tests

    [Fact]
    public async Task GetHistoryAsync_Should_ReturnEmptyList_When_NoThread()
    {
        // Act
        var history = await _manager.GetHistoryAsync("non-existent-session", CancellationToken.None);

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_Should_ReturnAllMessages()
    {
        // Arrange
        var sessionId = "test-session-history";
        await _manager.AddMessageAsync(sessionId, "system", "You are helpful", CancellationToken.None);
        await _manager.AddMessageAsync(sessionId, "user", "Hello", CancellationToken.None);
        await _manager.AddMessageAsync(sessionId, "assistant", "Hi there!", CancellationToken.None);

        // Act
        var history = await _manager.GetHistoryAsync(sessionId, CancellationToken.None);

        // Assert
        history.Count.Should().Be(3);
        history[0].Role.Should().Be("system");
        history[1].Role.Should().Be("user");
        history[2].Role.Should().Be("assistant");
    }

    #endregion

    #region CleanupThreadAsync Tests

    [Fact]
    public async Task CleanupThreadAsync_Should_RemoveThread()
    {
        // Arrange
        var sessionId = "test-session-cleanup";
        await _manager.AddMessageAsync(sessionId, "user", "Hello", CancellationToken.None);

        _manager.HasActiveThread(sessionId).Should().BeTrue();

        // Act
        await _manager.CleanupThreadAsync(sessionId, false, CancellationToken.None);

        // Assert
        _manager.HasActiveThread(sessionId).Should().BeFalse();
    }

    [Fact]
    public async Task CleanupThreadAsync_Should_PersistToRedis_When_Requested()
    {
        // Arrange
        var sessionId = "test-session-persist";
        await _manager.AddMessageAsync(sessionId, "user", "Hello", CancellationToken.None);
        await _manager.AddMessageAsync(sessionId, "assistant", "Hi!", CancellationToken.None);

        // Act
        await _manager.CleanupThreadAsync(sessionId, persistToRedis: true, CancellationToken.None);

        // Assert
        _cacheMock.Verify(c => c.SetStringAsync(
            $"chat_history:{sessionId}",
            It.IsAny<string>(),
            It.Is<TimeSpan?>(t => t == TimeSpan.FromDays(7)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CleanupThreadAsync_Should_NotPersist_When_NotRequested()
    {
        // Arrange
        var sessionId = "test-session-no-persist";
        await _manager.AddMessageAsync(sessionId, "user", "Hello", CancellationToken.None);

        // Act
        await _manager.CleanupThreadAsync(sessionId, persistToRedis: false, CancellationToken.None);

        // Assert
        _cacheMock.Verify(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupThreadAsync_Should_DoNothing_When_ThreadDoesNotExist()
    {
        // Act - should not throw
        await _manager.CleanupThreadAsync("non-existent", false, CancellationToken.None);

        // Assert - no exception thrown
        _manager.HasActiveThread("non-existent").Should().BeFalse();
    }

    #endregion

    #region HasActiveThread Tests

    [Fact]
    public void HasActiveThread_Should_ReturnFalse_When_NoThread()
    {
        // Act & Assert
        _manager.HasActiveThread("non-existent").Should().BeFalse();
    }

    [Fact]
    public async Task HasActiveThread_Should_ReturnTrue_When_ThreadExists()
    {
        // Arrange
        var sessionId = "test-session-exists";
        await _manager.AddMessageAsync(sessionId, "user", "Hello", CancellationToken.None);

        // Act & Assert
        _manager.HasActiveThread(sessionId).Should().BeTrue();
    }

    [Fact]
    public void HasActiveThread_Should_ReturnFalse_When_SessionIdIsEmpty()
    {
        // Act & Assert
        _manager.HasActiveThread("").Should().BeFalse();
        _manager.HasActiveThread("   ").Should().BeFalse();
    }

    [Fact]
    public void HasActiveThread_Should_ReturnFalse_When_SessionIdIsNull()
    {
        // Act & Assert
        _manager.HasActiveThread(null!).Should().BeFalse();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task AddMessageAsync_Should_BeThreadSafe()
    {
        // Arrange
        var sessionId = "test-session-threadsafe";
        var messageCount = 100;
        var tasks = new List<Task>();

        // Act - add messages concurrently
        for (var i = 0; i < messageCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
                _manager.AddMessageAsync(sessionId, "user", $"Message {index}", CancellationToken.None)));
        }

        await Task.WhenAll(tasks);

        // Assert
        var history = await _manager.GetHistoryAsync(sessionId, CancellationToken.None);
        history.Count.Should().Be(messageCount);
    }

    #endregion

    #region GetActiveThreadCount Tests

    [Fact]
    public void GetActiveThreadCount_Should_ReturnZero_When_NoThreads()
    {
        // Act & Assert
        _manager.GetActiveThreadCount().Should().Be(0);
    }

    [Fact]
    public async Task GetActiveThreadCount_Should_ReturnCorrectCount()
    {
        // Arrange
        await _manager.AddMessageAsync("session-1", "user", "Hello", CancellationToken.None);
        await _manager.AddMessageAsync("session-2", "user", "World", CancellationToken.None);
        await _manager.AddMessageAsync("session-3", "user", "Test", CancellationToken.None);

        // Act & Assert
        _manager.GetActiveThreadCount().Should().Be(3);
    }

    [Fact]
    public async Task GetActiveThreadCount_Should_Decrease_After_Cleanup()
    {
        // Arrange
        await _manager.AddMessageAsync("session-cleanup-1", "user", "Hello", CancellationToken.None);
        await _manager.AddMessageAsync("session-cleanup-2", "user", "World", CancellationToken.None);

        // Act
        await _manager.CleanupThreadAsync("session-cleanup-1", false, CancellationToken.None);

        // Assert
        _manager.GetActiveThreadCount().Should().Be(1);
    }

    #endregion

    #region GetExpiredSessionIds Tests

    [Fact]
    public void GetExpiredSessionIds_Should_ReturnEmpty_When_NoThreads()
    {
        // Act
        var result = _manager.GetExpiredSessionIds(TimeSpan.FromMinutes(60));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExpiredSessionIds_Should_ReturnEmpty_When_AllThreadsActive()
    {
        // Arrange - Create fresh threads
        await _manager.AddMessageAsync("active-session-1", "user", "Hello", CancellationToken.None);
        await _manager.AddMessageAsync("active-session-2", "user", "World", CancellationToken.None);

        // Act - Check for threads idle > 60 minutes (none should be)
        var result = _manager.GetExpiredSessionIds(TimeSpan.FromMinutes(60));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExpiredSessionIds_Should_ReturnExpiredThreads_When_IdleTimeoutExceeded()
    {
        // Arrange - Create threads
        await _manager.AddMessageAsync("old-session", "user", "Hello", CancellationToken.None);

        // Act - Use a very short timeout so all threads appear expired
        var result = _manager.GetExpiredSessionIds(TimeSpan.Zero);

        // Assert
        result.Should().Contain("old-session");
    }

    [Fact]
    public async Task GetExpiredSessionIds_Should_NotIncludeRecentlyAccessedThreads()
    {
        // Arrange
        await _manager.AddMessageAsync("fresh-session", "user", "Hello", CancellationToken.None);

        // Act - Access the thread to refresh LastAccessedAt
        await _manager.GetHistoryAsync("fresh-session", CancellationToken.None);

        // Use timeout of 1 hour - thread should not be expired
        var result = _manager.GetExpiredSessionIds(TimeSpan.FromHours(1));

        // Assert
        result.Should().NotContain("fresh-session");
    }

    #endregion
}
