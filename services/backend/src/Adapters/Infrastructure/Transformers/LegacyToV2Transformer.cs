using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Transformers;

/// <summary>
/// Transforms legacy V1 ChatMessage records to V2 MessageV2Entity format.
/// Uses cache to track migration status per session.
/// </summary>
public sealed class LegacyToV2Transformer : IMessageTransformer
{
    private readonly ICacheStore _cache;
    private readonly ILogger<LegacyToV2Transformer> _logger;

    private const string MigrationKeyPrefix = "chat:v2:migrated:";
    private static readonly TimeSpan MigrationStatusExpiry = TimeSpan.FromDays(30);

    public LegacyToV2Transformer(
        ICacheStore cache,
        ILogger<LegacyToV2Transformer> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<List<MessageV2Entity>> TransformLegacyMessagesAsync(
        string sessionId,
        Guid chatSessionId,
        IReadOnlyList<ChatMessage> legacyMessages,
        CancellationToken cancellationToken = default)
    {
        var result = new List<MessageV2Entity>();
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (var i = 0; i < legacyMessages.Count; i++)
        {
            var legacy = legacyMessages[i];

            // Skip activity messages - they are not persisted in V2
            if (legacy.Role == "activity")
                continue;

            // Generate a deterministic timestamp based on order
            // This preserves message ordering when migrating
            var timestamp = baseTimestamp - ((legacyMessages.Count - i) * 1000);

            var entity = new MessageV2Entity
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ChatSessionId = chatSessionId,
                Role = MapRole(legacy.Role),
                Content = legacy.Content,
                ToolCallsJson = SerializeToolCalls(legacy.ToolCalls),
                ArtifactsJson = null, // V1 didn't have artifacts
                ReactionsJson = null, // V1 didn't have reactions
                Timestamp = timestamp,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            result.Add(entity);
        }

        _logger.LogInformation(
            "Transformed {Count} legacy messages for session {SessionId}",
            result.Count,
            sessionId);

        return Task.FromResult(result);
    }

    public async Task<bool> IsSessionMigratedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var key = $"{MigrationKeyPrefix}{sessionId}";
        var value = await _cache.GetStringAsync(key, cancellationToken);
        return value == "true";
    }

    public async Task MarkSessionMigratedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var key = $"{MigrationKeyPrefix}{sessionId}";
        await _cache.SetStringAsync(key, "true", MigrationStatusExpiry, cancellationToken);

        _logger.LogInformation(
            "Session {SessionId} marked as migrated to V2",
            sessionId);
    }

    private static string MapRole(string legacyRole)
    {
        return legacyRole switch
        {
            "user" => "user",
            "assistant" => "assistant",
            "system" => "system",
            _ => "user" // Default to user for unknown roles
        };
    }

    private static string? SerializeToolCalls(object? toolCalls)
    {
        if (toolCalls is null)
            return null;

        try
        {
            // ToolCalls from ChatMessage is already an object, serialize it
            return JsonSerializer.Serialize(toolCalls, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch
        {
            return null;
        }
    }
}
