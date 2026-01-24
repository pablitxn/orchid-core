using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.ChatV2.Common;
using Core.Application.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.ChatV2.ToggleReaction;

public sealed class ToggleReactionHandler : IRequestHandler<ToggleReactionCommand, ToggleReactionDto>
{
    private readonly IMessageRepository _messageRepository;
    private readonly IChatSessionRepository _sessionRepository;
    private readonly ILogger<ToggleReactionHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ToggleReactionHandler(
        IMessageRepository messageRepository,
        IChatSessionRepository sessionRepository,
        ILogger<ToggleReactionHandler> logger)
    {
        _messageRepository = messageRepository;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task<ToggleReactionDto> Handle(
        ToggleReactionCommand request,
        CancellationToken cancellationToken)
    {
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

        // Get the message
        var message = await _messageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        if (message is null)
        {
            throw new DocumentNotFoundException(request.MessageId);
        }

        // Verify message belongs to the session
        if (message.SessionId != request.SessionId)
        {
            throw new UnauthorizedDomainAccessException("Message", request.MessageId, request.UserId);
        }

        // Deserialize existing reactions
        var reactions = DeserializeReactions(message.ReactionsJson);

        // Check if user already has this reaction
        var existingReaction = reactions.FirstOrDefault(r =>
            r.UserId == request.UserId.ToString() && r.Emoji == request.Emoji);

        bool wasAdded;
        ReactionDto? resultReaction = null;

        if (existingReaction is not null)
        {
            // Remove the reaction
            reactions.Remove(existingReaction);
            wasAdded = false;

            _logger.LogInformation(
                "Removed reaction {Emoji} from message {MessageId} by user {UserId}",
                request.Emoji,
                request.MessageId,
                request.UserId);
        }
        else
        {
            // Add the reaction
            resultReaction = new ReactionDto
            {
                UserId = request.UserId.ToString(),
                Emoji = request.Emoji,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            reactions.Add(resultReaction);
            wasAdded = true;

            _logger.LogInformation(
                "Added reaction {Emoji} to message {MessageId} by user {UserId}",
                request.Emoji,
                request.MessageId,
                request.UserId);
        }

        // Serialize and update the message
        message.ReactionsJson = SerializeReactions(reactions);
        message.UpdatedAt = DateTime.UtcNow;

        await _messageRepository.UpdateAsync(message, cancellationToken);

        return new ToggleReactionDto(
            request.MessageId.ToString(),
            resultReaction,
            wasAdded);
    }

    private static List<ReactionDto> DeserializeReactions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<ReactionDto>();

        try
        {
            return JsonSerializer.Deserialize<List<ReactionDto>>(json, JsonOptions) ?? new List<ReactionDto>();
        }
        catch
        {
            return new List<ReactionDto>();
        }
    }

    private static string? SerializeReactions(List<ReactionDto> reactions)
    {
        if (reactions.Count == 0)
            return null;

        return JsonSerializer.Serialize(reactions, JsonOptions);
    }
}
