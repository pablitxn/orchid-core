using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.ChatV2.Common;
using Core.Application.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.ChatV2.ListMessages;

public sealed class ListMessagesV2Handler : IRequestHandler<ListMessagesV2Query, PaginatedMessagesDto>
{
    private readonly IMessageRepository _messageRepository;
    private readonly IChatSessionRepository _sessionRepository;
    private readonly IMessageTransformer _transformer;
    private readonly ILogger<ListMessagesV2Handler> _logger;

    private const int MaxLimit = 100;

    public ListMessagesV2Handler(
        IMessageRepository messageRepository,
        IChatSessionRepository sessionRepository,
        IMessageTransformer transformer,
        ILogger<ListMessagesV2Handler> logger)
    {
        _messageRepository = messageRepository;
        _sessionRepository = sessionRepository;
        _transformer = transformer;
        _logger = logger;
    }

    public async Task<PaginatedMessagesDto> Handle(
        ListMessagesV2Query request,
        CancellationToken cancellationToken)
    {
        // Validate limit
        var limit = Math.Min(Math.Max(1, request.Limit), MaxLimit);

        // Verify session exists and user has access
        var session = await _sessionRepository.GetBySessionIdAsync(request.SessionId, cancellationToken);
        if (session is null)
        {
            throw new ChatSessionNotFoundException(request.SessionId);
        }

        if (session.UserId != request.UserId)
        {
            throw new UnauthorizedDomainAccessException("ChatSession", session.Id, request.UserId);
        }

        // Check if session needs V1->V2 migration
        var isMigrated = await _transformer.IsSessionMigratedAsync(request.SessionId, cancellationToken);
        if (!isMigrated)
        {
            _logger.LogInformation(
                "Session {SessionId} not yet migrated to V2, will return empty result pending migration",
                request.SessionId);
            // For now, return empty result for non-migrated sessions
            // A background job or on-demand migration can populate this later
            return new PaginatedMessagesDto
            {
                Messages = new List<MessageV2Dto>(),
                NextCursor = null,
                PreviousCursor = null,
                HasMore = false,
                TotalCount = 0
            };
        }

        // Parse cursor if provided
        CursorDto? cursor = null;
        if (!string.IsNullOrEmpty(request.Cursor))
        {
            cursor = DecodeCursor(request.Cursor);
        }

        // Fetch messages with pagination
        long? beforeTimestamp = null;
        long? afterTimestamp = null;
        Guid? cursorMessageId = cursor is not null ? Guid.Parse(cursor.MessageId) : null;

        if (request.Direction == "backward")
        {
            beforeTimestamp = cursor?.Timestamp;
        }
        else
        {
            afterTimestamp = cursor?.Timestamp;
        }

        // Fetch one extra to determine if there are more
        var messages = await _messageRepository.GetBySessionIdAsync(
            request.SessionId,
            limit + 1,
            beforeTimestamp,
            afterTimestamp,
            cursorMessageId,
            cancellationToken);

        var hasMore = messages.Count > limit;
        if (hasMore)
        {
            messages = messages.Take(limit).ToList();
        }

        // Get total count
        var totalCount = await _messageRepository.GetCountBySessionIdV2Async(request.SessionId, cancellationToken);

        // Map entities to DTOs
        var messageDtos = messages.Select(MapToDto).ToList();

        // Calculate cursors
        string? nextCursor = null;
        string? previousCursor = null;

        if (messageDtos.Count > 0)
        {
            if (hasMore)
            {
                var lastMessage = messageDtos.Last();
                nextCursor = EncodeCursor(new CursorDto
                {
                    Timestamp = lastMessage.Timestamp,
                    MessageId = lastMessage.Id
                });
            }

            if (cursor is not null)
            {
                var firstMessage = messageDtos.First();
                previousCursor = EncodeCursor(new CursorDto
                {
                    Timestamp = firstMessage.Timestamp,
                    MessageId = firstMessage.Id
                });
            }
        }

        return new PaginatedMessagesDto
        {
            Messages = messageDtos,
            NextCursor = nextCursor,
            PreviousCursor = previousCursor,
            HasMore = hasMore,
            TotalCount = totalCount
        };
    }

    private static MessageV2Dto MapToDto(Domain.Entities.MessageV2Entity entity)
    {
        return new MessageV2Dto
        {
            Id = entity.Id.ToString(),
            Role = entity.Role,
            Content = entity.Content,
            ToolCalls = DeserializeJson<List<ToolCallDto>>(entity.ToolCallsJson),
            Artifacts = DeserializeJson<List<ArtifactDto>>(entity.ArtifactsJson),
            Reactions = DeserializeJson<List<ReactionDto>>(entity.ReactionsJson),
            Timestamp = entity.Timestamp
        };
    }

    private static T? DeserializeJson<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch
        {
            return null;
        }
    }

    private static string EncodeCursor(CursorDto cursor)
    {
        var json = JsonSerializer.Serialize(cursor, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static CursorDto? DecodeCursor(string encoded)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return JsonSerializer.Deserialize<CursorDto>(json, new JsonSerializerOptions
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
