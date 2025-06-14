using System.Globalization;
using System.Linq;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CreditSystem.IntegrationTests.Mocks;

public class MockChatCompletionPort : IChatCompletionPort
{
    public Task<string> CompleteAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.LastOrDefault(m => m.Role == "user");
        return Task.FromResult($"Mock AI response to: {lastMessage?.Content ?? "empty"}");
    }

    public Task<string> CompleteWithAgentAsync(IEnumerable<ChatMessage> messages, Guid agentId, 
        Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var lastMessage = messages.LastOrDefault(m => m.Role == "user");
        return Task.FromResult($"Mock AI response with agent {agentId} to: {lastMessage?.Content ?? "empty"}");
    }
}

public class MockTokenCounter : ITokenCounter
{
    public int CountTokens(string text)
    {
        // Simple mock implementation - estimate 1 token per 4 characters
        return string.IsNullOrEmpty(text) ? 0 : text.Length / 4;
    }
}

public class MockChatSessionRepository : IChatSessionRepository
{
    private readonly Dictionary<string, ChatSessionEntity> _sessions = new();
    private readonly Dictionary<Guid, ChatSessionEntity> _sessionsByGuid = new();

    public Task<List<ChatSessionEntity>> ListAsync(
        Guid userId,
        bool archived,
        Guid? agentId,
        Guid? teamId,
        DateTime? startDate,
        DateTime? endDate,
        InteractionType? type,
        CancellationToken cancellationToken)
    {
        var query = _sessions.Values.Where(s => s.UserId == userId && s.IsArchived == archived);
        
        if (agentId.HasValue)
            query = query.Where(s => s.AgentId == agentId.Value);
        
        if (teamId.HasValue)
            query = query.Where(s => s.TeamId == teamId.Value);
        
        if (startDate.HasValue)
            query = query.Where(s => s.CreatedAt >= startDate.Value);
        
        if (endDate.HasValue)
            query = query.Where(s => s.CreatedAt <= endDate.Value);
        
        if (type.HasValue)
            query = query.Where(s => s.InteractionType == type.Value);
        
        return Task.FromResult(query.ToList());
    }

    public Task<List<ChatSessionEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = _sessions.Values.Where(s => s.UserId == userId).ToList();
        return Task.FromResult(sessions);
    }

    public Task<ChatSessionEntity?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task CreateAsync(ChatSessionEntity session, CancellationToken cancellationToken)
    {
        _sessions[session.SessionId] = session;
        _sessionsByGuid[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task ArchiveAsync(Guid id, bool archived, CancellationToken cancellationToken)
    {
        if (_sessionsByGuid.TryGetValue(id, out var session))
        {
            session.IsArchived = archived;
            session.UpdatedAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_sessionsByGuid.TryGetValue(id, out var session))
        {
            _sessions.Remove(session.SessionId);
            _sessionsByGuid.Remove(id);
        }
        return Task.CompletedTask;
    }

    public Task DeleteManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        foreach (var id in ids)
        {
            if (_sessionsByGuid.TryGetValue(id, out var session))
            {
                _sessions.Remove(session.SessionId);
                _sessionsByGuid.Remove(id);
            }
        }
        return Task.CompletedTask;
    }

    // Helper method for tests
    public void AddSession(ChatSessionEntity session)
    {
        _sessions[session.SessionId] = session;
        _sessionsByGuid[session.Id] = session;
    }
}

public class MockStringLocalizer<T> : IStringLocalizer<T>
{
    private readonly Dictionary<string, string> _localizations = new()
    {
        ["UserNotRecognized"] = "User not recognized",
        ["SubscriptionNotFound"] = "Subscription not found", 
        ["InsufficientCredits"] = "Insufficient credits",
        ["ChatSessionNotFound"] = "Chat session not found",
        ["UnauthorizedAccess"] = "Unauthorized access",
        ["AgentNotFound"] = "Agent not found",
        ["SessionNotFound"] = "Session not found",
        ["NoAgentSelected"] = "No agent selected",
        ["AgentAccessDenied"] = "Agent access denied",
        ["MissingPlugins"] = "Missing plugins",
        ["AiTimeout"] = "AI timeout",
        ["AiError"] = "AI error: {0}",
        ["Welcome"] = "Welcome to the chat!"
    };

    public LocalizedString this[string name]
    {
        get
        {
            var value = _localizations.TryGetValue(name, out var v) ? v : name;
            return new LocalizedString(name, value);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = _localizations.TryGetValue(name, out var v) ? v : name;
            return new LocalizedString(name, string.Format(value, arguments));
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return _localizations.Select(kv => new LocalizedString(kv.Key, kv.Value));
    }
}

public class MockActivityPublisher : IActivityPublisher
{
    private readonly ILogger<MockActivityPublisher> _logger;

    public MockActivityPublisher(ILogger<MockActivityPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(string type, object? payload = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock activity published: {Type}", type);
        return Task.CompletedTask;
    }
}