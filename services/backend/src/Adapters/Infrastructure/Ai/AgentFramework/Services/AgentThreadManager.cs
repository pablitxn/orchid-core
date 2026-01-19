using System.Collections.Concurrent;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Manages conversation threads for agents, handling migration from Redis-based history.
/// Thread-safe implementation using ConcurrentDictionary for active thread storage.
/// </summary>
public sealed class AgentThreadManager : IAgentThreadManager
{
    private readonly ICacheStore _cache;
    private readonly ILogger<AgentThreadManager> _logger;

    /// <summary>
    /// In-memory storage for active conversation threads.
    /// Key: sessionId, Value: thread data including messages and metadata.
    /// </summary>
    private readonly ConcurrentDictionary<string, ThreadData> _activeThreads = new();

    /// <summary>
    /// Tracks which sessions have been migrated to prevent duplicate migrations.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _migratedSessions = new();

    /// <summary>
    /// Redis key prefix for legacy chat history.
    /// </summary>
    private const string RedisHistoryKeyPrefix = "chat_history:";

    /// <summary>
    /// Redis key prefix for activity history.
    /// </summary>
    private const string ActivityHistoryKeyPrefix = "activity_history:";

    public AgentThreadManager(
        ICacheStore cache,
        ILogger<AgentThreadManager> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AgentThreadInfo> GetOrCreateThreadAsync(
        string sessionId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Check if we already have an active thread
        if (_activeThreads.TryGetValue(sessionId, out var existingThread))
        {
            _logger.LogDebug(
                "Returning existing thread for session {SessionId} with {MessageCount} messages",
                sessionId, existingThread.Messages.Count);

            return new AgentThreadInfo(
                sessionId,
                existingThread.Messages.Count,
                existingThread.WasMigrated,
                existingThread.CreatedAt);
        }

        // Create new thread data
        var threadData = new ThreadData
        {
            SessionId = sessionId,
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow,
            Messages = new List<ChatMessage>()
        };

        // Check for and migrate Redis history if needed
        var wasMigrated = await MigrateFromRedisInternalAsync(sessionId, threadData, cancellationToken);
        threadData.WasMigrated = wasMigrated;

        // Store the thread (thread-safe add or update)
        _activeThreads.AddOrUpdate(sessionId, threadData, (_, existing) => existing);

        _logger.LogInformation(
            "Created new thread for session {SessionId}, agent {AgentId}. Migrated: {WasMigrated}, Messages: {MessageCount}",
            sessionId, agentId, wasMigrated, threadData.Messages.Count);

        return new AgentThreadInfo(
            sessionId,
            threadData.Messages.Count,
            wasMigrated,
            threadData.CreatedAt);
    }

    /// <inheritdoc />
    public async Task<bool> MigrateFromRedisIfNeededAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Check if already migrated
        if (_migratedSessions.ContainsKey(sessionId))
        {
            _logger.LogDebug("Session {SessionId} already migrated, skipping", sessionId);
            return false;
        }

        // Get or create thread data
        if (!_activeThreads.TryGetValue(sessionId, out var threadData))
        {
            threadData = new ThreadData
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                Messages = new List<ChatMessage>()
            };
            _activeThreads.TryAdd(sessionId, threadData);
        }

        return await MigrateFromRedisInternalAsync(sessionId, threadData, cancellationToken);
    }

    /// <inheritdoc />
    public Task AddMessageAsync(
        string sessionId,
        string role,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        if (!_activeThreads.TryGetValue(sessionId, out var threadData))
        {
            // Create thread data if it doesn't exist
            threadData = new ThreadData
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                Messages = new List<ChatMessage>()
            };
            _activeThreads.TryAdd(sessionId, threadData);
            _activeThreads.TryGetValue(sessionId, out threadData);
        }

        var message = new ChatMessage(role, content);

        lock (threadData!.Messages)
        {
            threadData.Messages.Add(message);
        }

        _logger.LogDebug(
            "Added {Role} message to session {SessionId}. Total messages: {Count}",
            role, sessionId, threadData.Messages.Count);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!_activeThreads.TryGetValue(sessionId, out var threadData))
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(Array.Empty<ChatMessage>());
        }

        lock (threadData.Messages)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(threadData.Messages.ToList().AsReadOnly());
        }
    }

    /// <inheritdoc />
    public async Task CleanupThreadAsync(
        string sessionId,
        bool persistToRedis = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!_activeThreads.TryRemove(sessionId, out var threadData))
        {
            _logger.LogDebug("No active thread found for session {SessionId} to cleanup", sessionId);
            return;
        }

        // Optionally persist to Redis as backup before cleanup
        if (persistToRedis && threadData.Messages.Count > 0)
        {
            var redisKey = $"{RedisHistoryKeyPrefix}{sessionId}";
            try
            {
                await _cache.SetAsync(
                    redisKey,
                    threadData.Messages.ToList(),
                    TimeSpan.FromDays(7), // Longer retention for archived sessions
                    cancellationToken);

                _logger.LogInformation(
                    "Persisted {MessageCount} messages to Redis for archived session {SessionId}",
                    threadData.Messages.Count, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to persist session {SessionId} to Redis during cleanup",
                    sessionId);
            }
        }

        // Remove from migrated sessions tracking
        _migratedSessions.TryRemove(sessionId, out _);

        _logger.LogInformation(
            "Cleaned up thread for session {SessionId}. Messages: {MessageCount}, Persisted: {Persisted}",
            sessionId, threadData.Messages.Count, persistToRedis);
    }

    /// <inheritdoc />
    public bool HasActiveThread(string sessionId)
    {
        return !string.IsNullOrWhiteSpace(sessionId) && _activeThreads.ContainsKey(sessionId);
    }

    /// <summary>
    /// Internal method to migrate Redis history to thread data.
    /// Idempotent - will not duplicate messages if called multiple times.
    /// </summary>
    private async Task<bool> MigrateFromRedisInternalAsync(
        string sessionId,
        ThreadData threadData,
        CancellationToken cancellationToken)
    {
        // Check if already migrated for this session
        if (_migratedSessions.ContainsKey(sessionId))
        {
            return false;
        }

        var redisKey = $"{RedisHistoryKeyPrefix}{sessionId}";

        try
        {
            var redisHistory = await _cache.GetAsync<List<ChatMessage>>(redisKey, cancellationToken);

            if (redisHistory == null || redisHistory.Count == 0)
            {
                _logger.LogDebug("No Redis history found for session {SessionId}", sessionId);
                _migratedSessions.TryAdd(sessionId, true);
                return false;
            }

            // Replay messages into thread (only user/assistant/system messages)
            var migratedCount = 0;
            lock (threadData.Messages)
            {
                foreach (var msg in redisHistory)
                {
                    if (msg.Role is "user" or "assistant" or "system")
                    {
                        threadData.Messages.Add(msg);
                        migratedCount++;
                    }
                }
            }

            // Mark as migrated
            _migratedSessions.TryAdd(sessionId, true);

            _logger.LogInformation(
                "Migrated {MigratedCount} messages from Redis to AgentThread for session {SessionId}",
                migratedCount, sessionId);

            // Optionally remove from Redis after successful migration
            // Keeping it as backup for now - uncomment to delete after migration
            // await _cache.RemoveAsync(redisKey, cancellationToken);

            return migratedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error migrating Redis history for session {SessionId}",
                sessionId);
            return false;
        }
    }

    /// <summary>
    /// Internal thread data structure.
    /// </summary>
    private sealed class ThreadData
    {
        public required string SessionId { get; init; }
        public Guid AgentId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required List<ChatMessage> Messages { get; init; }
        public bool WasMigrated { get; set; }
    }
}
