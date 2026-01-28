using Application.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.ChatSession.ChangeSessionModel;

public sealed class ChangeSessionModelHandler : IRequestHandler<ChangeSessionModelCommand, ChangeSessionModelDto>
{
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IModelResolver _modelResolver;
    private readonly IModelChangeNotificationPort _notificationPort;
    private readonly ILogger<ChangeSessionModelHandler> _logger;

    public ChangeSessionModelHandler(
        IChatSessionRepository chatSessionRepository,
        IModelResolver modelResolver,
        IModelChangeNotificationPort notificationPort,
        ILogger<ChangeSessionModelHandler> logger)
    {
        _chatSessionRepository = chatSessionRepository;
        _modelResolver = modelResolver;
        _notificationPort = notificationPort;
        _logger = logger;
    }

    public async Task<ChangeSessionModelDto> Handle(
        ChangeSessionModelCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Get the session and verify ownership
        var session = await _chatSessionRepository.GetBySessionIdAsync(
            request.SessionId, cancellationToken);

        if (session is null)
        {
            throw new ChatSessionNotFoundException(request.SessionId);
        }

        if (session.UserId != request.UserId)
        {
            throw new UnauthorizedDomainAccessException(
                "ChatSession", session.Id, request.UserId);
        }

        // 2. Validate the model exists
        if (!_modelResolver.IsValidModel(request.ModelId))
        {
            throw new ValidationException(
                $"Invalid model ID: {request.ModelId}", "modelId");
        }

        // 3. Get previous model for notification
        var previousModel = session.ModelOverride;

        // 4. Update the session
        var updatedSession = await _chatSessionRepository.UpdateModelOverrideAsync(
            request.SessionId,
            request.ModelId,
            cancellationToken);

        if (updatedSession is null)
        {
            throw new OperationFailedException(
                "Failed to update session model override");
        }

        // 5. Send notification via SignalR
        await _notificationPort.NotifyModelChangedAsync(
            request.SessionId,
            previousModel,
            request.ModelId,
            request.UserId,
            cancellationToken);

        _logger.LogInformation(
            "Session model changed: {SessionId}, {PreviousModel} -> {NewModel}, by User {UserId}",
            request.SessionId, previousModel ?? "(none)", request.ModelId, request.UserId);

        return new ChangeSessionModelDto
        {
            SessionId = request.SessionId,
            PreviousModel = previousModel,
            CurrentModel = request.ModelId,
            ChangedAt = updatedSession.ModelChangedAt ?? DateTime.UtcNow
        };
    }
}
