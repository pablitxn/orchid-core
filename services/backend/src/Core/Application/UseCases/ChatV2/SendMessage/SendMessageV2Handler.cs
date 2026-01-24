using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.ChatV2.Common;
using Core.Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.ChatV2.SendMessage;

public sealed class SendMessageV2Handler : IRequestHandler<SendMessageV2Command, MessageV2Dto>
{
    private readonly IMessageRepository _messageRepository;
    private readonly IChatSessionRepository _sessionRepository;
    private readonly IMessageTransformer _transformer;
    private readonly ILogger<SendMessageV2Handler> _logger;

    public SendMessageV2Handler(
        IMessageRepository messageRepository,
        IChatSessionRepository sessionRepository,
        IMessageTransformer transformer,
        ILogger<SendMessageV2Handler> logger)
    {
        _messageRepository = messageRepository;
        _sessionRepository = sessionRepository;
        _transformer = transformer;
        _logger = logger;
    }

    public async Task<MessageV2Dto> Handle(
        SendMessageV2Command request,
        CancellationToken cancellationToken)
    {
        // Validate content
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new EmptyContentException();
        }

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

        // Ensure session is marked as migrated (first V2 message triggers migration status)
        var isMigrated = await _transformer.IsSessionMigratedAsync(request.SessionId, cancellationToken);
        if (!isMigrated)
        {
            await _transformer.MarkSessionMigratedAsync(request.SessionId, cancellationToken);
            _logger.LogInformation(
                "Session {SessionId} marked as migrated to V2 on first message",
                request.SessionId);
        }

        // Create the message entity
        var messageId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var message = new MessageV2Entity
        {
            Id = messageId,
            SessionId = request.SessionId,
            ChatSessionId = session.Id,
            Role = "user",
            Content = request.Content,
            ArtifactsJson = SerializeArtifacts(request.Artifacts),
            Timestamp = timestamp,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Persist the message
        await _messageRepository.CreateAsync(message, cancellationToken);

        _logger.LogInformation(
            "Created V2 message {MessageId} in session {SessionId}",
            messageId,
            request.SessionId);

        // Return the created message DTO
        return new MessageV2Dto
        {
            Id = messageId.ToString(),
            Role = "user",
            Content = request.Content,
            ToolCalls = null,
            Artifacts = request.Artifacts,
            Reactions = null,
            Timestamp = timestamp
        };
    }

    private static string? SerializeArtifacts(List<ArtifactDto>? artifacts)
    {
        if (artifacts is null || artifacts.Count == 0)
            return null;

        return JsonSerializer.Serialize(artifacts, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
