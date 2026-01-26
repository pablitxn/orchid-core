using Application.Interfaces;
using Application.UseCases.Agent.VerifyAgentAccess;
using Application.UseCases.Subscription.ConsumeCredits;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Matrix.SendMessage;

public sealed class SendMatrixMessageHandler(
    IMatrixUserLinkRepository matrixUserLinkRepository,
    ISubscriptionRepository subscriptionRepository,
    IRateLimitService rateLimitService,
    IAgentThreadManager threadManager,
    IChatCompletionPort chatCompletion,
    IMediator mediator,
    ILogger<SendMatrixMessageHandler> logger)
    : IRequestHandler<SendMatrixMessageCommand, SendMatrixMessageDto>
{
    private const int MaxMessagesPerMinute = 10;
    private const int DefaultCreditsPerMessage = 1;
    private const int MaxMessageLength = 10000;

    public async Task<SendMatrixMessageDto> Handle(
        SendMatrixMessageCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate message length
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return SendMatrixMessageDto.ProcessingError("Message content cannot be empty");
        }

        if (request.Content.Length > MaxMessageLength)
        {
            return SendMatrixMessageDto.ProcessingError($"Message exceeds maximum length of {MaxMessageLength} characters");
        }

        // 2. Look up the user by MatrixUserId
        var userLink = await matrixUserLinkRepository.GetByMatrixUserIdWithUserAsync(
            request.MatrixUserId,
            cancellationToken);

        if (userLink is not { IsActive: true })
        {
            logger.LogInformation(
                "Matrix message from unlinked user {MatrixUserId}",
                request.MatrixUserId);
            return SendMatrixMessageDto.UserNotLinked();
        }

        var userId = userLink.UserId;
        var userName = userLink.User?.Name ?? userLink.User?.Email ?? "Unknown";

        logger.LogInformation(
            "Processing Matrix message from {MatrixUserId} (User: {UserId}) to Agent {AgentId} in Room {RoomId}",
            request.MatrixUserId,
            userId,
            request.AgentId,
            request.RoomId);

        // 3. Check rate limit for this user
        var rateLimitKey = $"matrix_rate:{userId}";
        var rateLimitResult = await rateLimitService.CheckAsync(
            rateLimitKey,
            MaxMessagesPerMinute,
            TimeSpan.FromMinutes(1),
            cancellationToken);

        if (!rateLimitResult.IsAllowed)
        {
            logger.LogWarning(
                "Rate limit exceeded for Matrix user {MatrixUserId} (User: {UserId})",
                request.MatrixUserId,
                userId);
            return SendMatrixMessageDto.RateLimited((int)rateLimitResult.RetryAfter.TotalSeconds);
        }

        // 4. Get subscription and check credits
        var subscription = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (subscription == null)
        {
            logger.LogWarning(
                "No subscription found for user {UserId} (Matrix: {MatrixUserId})",
                userId,
                request.MatrixUserId);
            return SendMatrixMessageDto.InsufficientCredits(0);
        }

        var hasUnlimitedCredits = subscription.HasUnlimitedCredits();
        if (!hasUnlimitedCredits && subscription.Credits < DefaultCreditsPerMessage)
        {
            logger.LogInformation(
                "Insufficient credits for user {UserId}. Available: {Credits}",
                userId,
                subscription.Credits);
            return SendMatrixMessageDto.InsufficientCredits(subscription.Credits);
        }

        // 5. Verify access to the agent
        var accessResult = await mediator.Send(
            new VerifyAgentAccessQuery(userId, request.AgentId),
            cancellationToken);

        if (!accessResult.HasAccess)
        {
            logger.LogWarning(
                "User {UserId} denied access to agent {AgentId}. Reason: {Reason}",
                userId,
                request.AgentId,
                accessResult.Reason);
            return SendMatrixMessageDto.AgentAccessDenied(accessResult.Reason);
        }

        // 6. Generate session ID from roomId + agentId
        // This ensures each room-agent combination has its own conversation thread
        var sessionId = GenerateSessionId(request.RoomId, request.AgentId);

        // 7. Get or create thread for this session
        await threadManager.GetOrCreateThreadAsync(sessionId, request.AgentId, cancellationToken);

        // 8. Add user message to thread
        await threadManager.AddMessageAsync(sessionId, "user", request.Content, cancellationToken);

        // 9. Get conversation history and process with agent
        var history = await threadManager.GetHistoryAsync(sessionId, cancellationToken);
        var llmHistory = history.Where(m => m.Role is "user" or "assistant" or "system").ToList();

        string response;
        try
        {
            response = await chatCompletion.CompleteWithAgentAsync(
                llmHistory,
                request.AgentId,
                userId,
                cancellationToken);
        }
        catch (AgentNotFoundException)
        {
            logger.LogWarning("Agent {AgentId} not found", request.AgentId);
            return SendMatrixMessageDto.AgentNotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message with agent {AgentId}", request.AgentId);
            return SendMatrixMessageDto.ProcessingError("Error processing message with agent");
        }

        // 10. Add assistant response to thread
        await threadManager.AddMessageAsync(sessionId, "assistant", response, cancellationToken);

        // 11. Consume credits
        int creditsUsed = DefaultCreditsPerMessage;
        int creditsRemaining;

        try
        {
            var consumeResult = await mediator.Send(
                new ConsumeCreditsCommand(userId, creditsUsed, "matrix_message", sessionId),
                cancellationToken);
            creditsRemaining = consumeResult.Credits;
        }
        catch (InsufficientCreditsException)
        {
            // This shouldn't happen since we checked earlier, but handle it gracefully
            // The message was already processed, so we log and return success with 0 remaining
            logger.LogWarning(
                "Credits consumed by another process for user {UserId} during Matrix message processing",
                userId);
            creditsRemaining = 0;
        }

        logger.LogInformation(
            "Successfully processed Matrix message. Session: {SessionId}, Credits used: {CreditsUsed}, Remaining: {CreditsRemaining}",
            sessionId,
            creditsUsed,
            creditsRemaining);

        return SendMatrixMessageDto.Succeeded(
            response,
            sessionId,
            creditsUsed,
            creditsRemaining);
    }

    /// <summary>
    /// Generates a deterministic session ID from room ID and agent ID.
    /// Format: matrix_{hash} where hash is derived from roomId + agentId
    /// </summary>
    private static string GenerateSessionId(string roomId, Guid agentId)
    {
        // Create a deterministic hash from the combination
        var combined = $"{roomId}:{agentId}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
        var hashString = Convert.ToBase64String(hashBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")[..16];

        return $"matrix_{hashString}";
    }
}
