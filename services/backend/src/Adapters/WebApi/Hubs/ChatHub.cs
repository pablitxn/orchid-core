using System.Security.Claims;
using System.Text;
using Application.Interfaces;
using Application.UseCases.Agent.VerifyAgentAccess;
using Application.UseCases.Subscription.ConsumeCredits;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Serilog.Context;
using WebApi.Configuration;
using WebApi.Middleware;

namespace WebApi.Hubs;

public sealed class ChatHub(
    IChatCompletionPort chatCompletion,
    ICacheStore cache,
    IAgentThreadManager threadManager,
    IMediator mediator,
    ITokenCounter tokenCounter,
    IChatSessionRepository chatSessionRepository,
    IStringLocalizer<ChatHub> localizer,
    IOptions<CreditSystemConfiguration> creditConfig,
    IDistributedLockService distributedLock,
    IRateLimitService rateLimitService,
    ICreditTrackingService creditTrackingService) : Hub
{
    private readonly ICacheStore _cache =
        cache ?? throw new ArgumentNullException(nameof(cache));

    private readonly IAgentThreadManager _threadManager =
        threadManager ?? throw new ArgumentNullException(nameof(threadManager));

    private readonly IChatCompletionPort _chatCompletion =
        chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));

    private readonly IStringLocalizer<ChatHub> _localizer =
        localizer ?? throw new ArgumentNullException(nameof(localizer));

    private readonly IMediator _mediator =
        mediator ?? throw new ArgumentNullException(nameof(mediator));

    private readonly ITokenCounter _tokenCounter =
        tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));

    private readonly IChatSessionRepository _chatSessionRepository =
        chatSessionRepository ?? throw new ArgumentNullException(nameof(chatSessionRepository));

    private readonly CreditSystemConfiguration _creditConfig =
        creditConfig?.Value ?? throw new ArgumentNullException(nameof(creditConfig));

    private readonly IDistributedLockService _distributedLock =
        distributedLock ?? throw new ArgumentNullException(nameof(distributedLock));

    private readonly IRateLimitService _rateLimitService =
        rateLimitService ?? throw new ArgumentNullException(nameof(rateLimitService));

    private readonly ICreditTrackingService _creditTrackingService =
        creditTrackingService ?? throw new ArgumentNullException(nameof(creditTrackingService));

    /// <summary>
    ///     Broadcasts a backend activity event to connected clients and stores
    ///     it in a separate activity history for the session.
    /// </summary>
    public async Task BroadcastActivity(string sessionId, string type, object? payload = null)
    {
        var activity = new Activity(type, payload);
        var activityKey = $"activity_history:{sessionId}";

        // Store activity in session-specific activity history
        try
        {
            var history = await _cache.GetAsync<List<Activity>>(activityKey) ?? new List<Activity>();
            history.Add(activity);
            await _cache.SetAsync(activityKey, history, TimeSpan.FromDays(1));
        }
        catch
        {
            // Failing to store the history must not prevent real-time broadcasting.
        }

        await Clients.All.SendAsync("ReceiveActivity", activity);
    }

    /// <summary>
    ///     Receives a message from a client for a specific session, sends it to the AI service, and returns the AI response.
    /// </summary>
    /// <param name="sessionId">The chat session identifier.</param>
    /// <param name="message">The user's message content.</param>
    /// <param name="byTokens">If true, credits are calculated based on token count.</param>
    /// <param name="agentId">Optional agent ID to override the session's default agent.</param>
    /// <param name="teamId">Optional team ID for team-based chat context.</param>
    public async Task SendMessage(string sessionId, string message, bool byTokens = false, Guid? agentId = null, Guid? teamId = null)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["InvalidSessionId"]);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(message))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["MessageCannotBeEmpty"]);
            return;
        }
        
        const int maxMessageLength = 10000;
        if (message.Length > maxMessageLength)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", 
                string.Format(_localizer["MessageTooLong"], maxMessageLength));
            return;
        }

        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["UserNotRecognized"]);
            return;
        }
        
        // Check rate limit - 10 messages per minute per user
        var rateLimitKey = $"chat_rate:{userId}";
        var rateLimitResult = await _rateLimitService.CheckAsync(rateLimitKey, 10, TimeSpan.FromMinutes(1), Context.ConnectionAborted);
        
        if (!rateLimitResult.IsAllowed)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", 
                string.Format(_localizer["RateLimitExceeded"], rateLimitResult.RetryAfter.TotalSeconds));
            return;
        }

        var credits = _creditConfig.MinimumCreditsPerMessage;
        if (byTokens)
        {
            var tokens = _tokenCounter.CountTokens(message);
            credits = Math.Max(_creditConfig.MinimumCreditsPerMessage,
                (int)Math.Ceiling((double)tokens / _creditConfig.TokensPerCredit));
        }

        try
        {
            await _mediator.Send(new ConsumeCreditsCommand(userId, credits, "message", sessionId), Context.ConnectionAborted);
        }
        catch (SubscriptionNotFoundException)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["SubscriptionNotFound"]);
            return;
        }
        catch (InsufficientCreditsException)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["InsufficientCredits"]);
            return;
        }

        // Track the message cost for detailed consumption history
        var messageId = Guid.NewGuid();
        await _creditTrackingService.TrackMessageCostAsync(
            userId,
            messageId,
            $"Chat message in session {sessionId}",
            credits,
            Context.ConnectionAborted);

        // Acquire distributed lock for this session to prevent race conditions
        var lockKey = $"chat_session:{sessionId}";
        await using var sessionLock = await _distributedLock.WaitAsync(
            lockKey,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
            Context.ConnectionAborted);

        // Get the chat session to retrieve the agent ID (needed for thread creation)
        var chatSession = await _chatSessionRepository.GetBySessionIdAsync(sessionId, Context.ConnectionAborted);
        if (chatSession == null)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["SessionNotFound"]);
            return;
        }
        
        // Verify session ownership
        if (chatSession.UserId != userId)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["AccessDenied"]);
            return;
        }

        // Use passed agentId if provided, otherwise fall back to session's agentId
        var effectiveAgentId = agentId ?? chatSession.AgentId;

        if (!effectiveAgentId.HasValue)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["NoAgentSelected"]);
            return;
        }

        // Verify user has access to the agent and its plugins
        var accessResult = await _mediator.Send(
            new VerifyAgentAccessQuery(userId, effectiveAgentId.Value),
            Context.ConnectionAborted);

        if (!accessResult.HasAccess)
        {
            var errorMessage = accessResult.Reason ?? _localizer["AgentAccessDenied"];
            if (accessResult.MissingPlugins?.Count > 0)
            {
                errorMessage += $" {_localizer["MissingPlugins"]}: {accessResult.MissingPlugins.Count}";
            }

            await Clients.Caller.SendAsync("ReceiveMessage", "bot", errorMessage);
            return;
        }

        // Get or create thread for this session (handles Redis migration automatically)
        await _threadManager.GetOrCreateThreadAsync(sessionId, effectiveAgentId.Value, Context.ConnectionAborted);

        // Add user message to thread
        await _threadManager.AddMessageAsync(sessionId, "user", message, Context.ConnectionAborted);

        // Broadcast the user's message as an activity
        await BroadcastActivity(sessionId, "user_message", new { sessionId, message });

        // Get conversation history for LLM (only user/assistant/system messages)
        var history = await _threadManager.GetHistoryAsync(sessionId, Context.ConnectionAborted);
        var llmHistory = history.Where(m => m.Role is "user" or "assistant" or "system").ToList();

        // Invoke the AI completion port with filtered history and agent ID
        // TODO: Implement team-based chat context when teamId is provided
        string response;
        try
        {
            response = await _chatCompletion.CompleteWithAgentAsync(llmHistory, effectiveAgentId.Value, userId, Context.ConnectionAborted);
        }
        catch (TaskCanceledException)
        {
            response = _localizer["AiTimeout"];
        }
        catch (Exception ex)
        {
            response = string.Format(_localizer["AiError"], ex.Message);
        }

        // Add AI response to thread
        await _threadManager.AddMessageAsync(sessionId, "assistant", response, Context.ConnectionAborted);

        // Send the AI response back to the caller
        await Clients.Caller.SendAsync("ReceiveMessage", "bot", response);
        // Broadcast the AI completion as an activity
        await BroadcastActivity(sessionId, "assistant_message", new { sessionId, message = response });
    }

    /// <summary>
    ///     Receives a message from a client for a specific session, streams the AI response token by token.
    ///     This is the streaming variant of SendMessage - the frontend receives tokens via ReceiveToken events.
    /// </summary>
    /// <param name="sessionId">The chat session identifier.</param>
    /// <param name="message">The user's message content.</param>
    /// <param name="byTokens">If true, credits are calculated based on token count.</param>
    /// <param name="agentId">Optional agent ID to override the session's default agent.</param>
    /// <param name="teamId">Optional team ID for team-based chat context.</param>
    public async Task SendMessageStreaming(string sessionId, string message, bool byTokens = false, Guid? agentId = null, Guid? teamId = null)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["InvalidSessionId"]);
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["MessageCannotBeEmpty"]);
            return;
        }

        const int maxMessageLength = 10000;
        if (message.Length > maxMessageLength)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot",
                string.Format(_localizer["MessageTooLong"], maxMessageLength));
            return;
        }

        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["UserNotRecognized"]);
            return;
        }

        // Check rate limit - 10 messages per minute per user
        var rateLimitKey = $"chat_rate:{userId}";
        var rateLimitResult = await _rateLimitService.CheckAsync(rateLimitKey, 10, TimeSpan.FromMinutes(1), Context.ConnectionAborted);

        if (!rateLimitResult.IsAllowed)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot",
                string.Format(_localizer["RateLimitExceeded"], rateLimitResult.RetryAfter.TotalSeconds));
            return;
        }

        var credits = _creditConfig.MinimumCreditsPerMessage;
        if (byTokens)
        {
            var tokens = _tokenCounter.CountTokens(message);
            credits = Math.Max(_creditConfig.MinimumCreditsPerMessage,
                (int)Math.Ceiling((double)tokens / _creditConfig.TokensPerCredit));
        }

        try
        {
            await _mediator.Send(new ConsumeCreditsCommand(userId, credits, "message", sessionId), Context.ConnectionAborted);
        }
        catch (SubscriptionNotFoundException)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["SubscriptionNotFound"]);
            return;
        }
        catch (InsufficientCreditsException)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["InsufficientCredits"]);
            return;
        }

        // Track the message cost for detailed consumption history
        var messageId = Guid.NewGuid();
        await _creditTrackingService.TrackMessageCostAsync(
            userId,
            messageId,
            $"Streaming chat message in session {sessionId}",
            credits,
            Context.ConnectionAborted);

        // Acquire distributed lock for this session to prevent race conditions
        var lockKey = $"chat_session:{sessionId}";
        await using var sessionLock = await _distributedLock.WaitAsync(
            lockKey,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
            Context.ConnectionAborted);

        // Get the chat session to retrieve the agent ID (needed for thread creation)
        var chatSession = await _chatSessionRepository.GetBySessionIdAsync(sessionId, Context.ConnectionAborted);
        if (chatSession == null)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["SessionNotFound"]);
            return;
        }

        // Verify session ownership
        if (chatSession.UserId != userId)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["AccessDenied"]);
            return;
        }

        // Use passed agentId if provided, otherwise fall back to session's agentId
        var effectiveAgentId = agentId ?? chatSession.AgentId;

        if (!effectiveAgentId.HasValue)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["NoAgentSelected"]);
            return;
        }

        // Verify user has access to the agent and its plugins
        var accessResult = await _mediator.Send(
            new VerifyAgentAccessQuery(userId, effectiveAgentId.Value),
            Context.ConnectionAborted);

        if (!accessResult.HasAccess)
        {
            var errorMessage = accessResult.Reason ?? _localizer["AgentAccessDenied"];
            if (accessResult.MissingPlugins?.Count > 0)
            {
                errorMessage += $" {_localizer["MissingPlugins"]}: {accessResult.MissingPlugins.Count}";
            }

            await Clients.Caller.SendAsync("ReceiveMessage", "bot", errorMessage);
            return;
        }

        // Get or create thread for this session (handles Redis migration automatically)
        await _threadManager.GetOrCreateThreadAsync(sessionId, effectiveAgentId.Value, Context.ConnectionAborted);

        // Add user message to thread
        await _threadManager.AddMessageAsync(sessionId, "user", message, Context.ConnectionAborted);

        // Broadcast the user's message as an activity
        await BroadcastActivity(sessionId, "user_message", new { sessionId, message });

        // Get conversation history for LLM (only user/assistant/system messages)
        var history = await _threadManager.GetHistoryAsync(sessionId, Context.ConnectionAborted);
        var llmHistory = history.Where(m => m.Role is "user" or "assistant" or "system").ToList();

        // Signal that streaming is starting
        await Clients.Caller.SendAsync("StreamStart", sessionId, Context.ConnectionAborted);

        // Stream the AI response
        var fullResponse = new StringBuilder();
        var streamErrored = false;

        try
        {
            await foreach (var token in _chatCompletion.CompleteWithAgentStreamingAsync(
                               llmHistory, effectiveAgentId.Value, userId, Context.ConnectionAborted))
            {
                fullResponse.Append(token);
                await Clients.Caller.SendAsync("ReceiveToken", token, Context.ConnectionAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // User disconnected or cancelled - this is expected behavior
            streamErrored = true;
            if (fullResponse.Length > 0)
            {
                // Save partial response
                await _threadManager.AddMessageAsync(sessionId, "assistant",
                    fullResponse + " [interrupted]", Context.ConnectionAborted);
            }
            return;
        }
        catch (Exception ex)
        {
            streamErrored = true;
            var errorResponse = string.Format(_localizer["AiError"], ex.Message);

            // If we have a partial response, append error notice
            if (fullResponse.Length > 0)
            {
                await _threadManager.AddMessageAsync(sessionId, "assistant",
                    fullResponse + $" [{errorResponse}]", CancellationToken.None);
            }

            // Notify client of the error
            await Clients.Caller.SendAsync("StreamError", errorResponse, CancellationToken.None);
            return;
        }

        // Signal that streaming has completed
        await Clients.Caller.SendAsync("StreamEnd", sessionId, Context.ConnectionAborted);

        // Add AI response to thread
        var response = fullResponse.ToString();
        await _threadManager.AddMessageAsync(sessionId, "assistant", response, Context.ConnectionAborted);

        // Send the complete message for compatibility with non-streaming clients
        await Clients.Caller.SendAsync("ReceiveMessage", "bot", response);

        // Broadcast the AI completion as an activity
        await BroadcastActivity(sessionId, "assistant_message", new { sessionId, message = response });
    }

    /// <summary>Returns the conversation history for the given session.</summary>
    public async Task<IReadOnlyList<ChatMessage>> GetHistory(string sessionId)
    {
        // Get chat history from thread manager (handles Redis migration)
        var chatHistory = await _threadManager.GetHistoryAsync(sessionId);

        // Get activity history from cache (still uses Redis for activities)
        var activityKey = $"activity_history:{sessionId}";
        var activityHistory = await _cache.GetAsync<List<Activity>>(activityKey) ?? new List<Activity>();

        // Convert activities to ChatMessages for frontend compatibility
        var activityMessages = activityHistory
            .Where(a => a.Type != "user_message" && a.Type != "assistant_message") // Exclude duplicates
            .Select(a => new ChatMessage("activity", string.Empty, a.Type, a.Payload))
            .ToList();

        // Merge chat messages and activities
        // Note: This is a simple merge. In production, you might want to merge by timestamp
        var result = new List<ChatMessage>();
        result.AddRange(chatHistory.Where(m => m.Role != "activity")); // Exclude old activity messages
        result.AddRange(activityMessages);

        return result;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var correlationId = GetCorrelationId();

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            var sessionId = Context.GetHttpContext()
                                ?.Request.Query["sessionId"].ToString()
                            ?? "unknown";

            // Notify everyone that this user disconnected
            await BroadcastActivity(
                sessionId,
                "user_disconnected",
                new { sessionId, user = Context.UserIdentifier }
            );

            await base.OnDisconnectedAsync(exception);
        }
    }

    public override async Task OnConnectedAsync()
    {
        var correlationId = GetCorrelationId();

        // Store correlation ID in connection items for future use
        Context.Items["CorrelationId"] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString() ?? "unknown";
            // Send a welcome message to the connecting user
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["Welcome"]);
            // Broadcast an activity to all users
            await BroadcastActivity(sessionId, "user_connected", new { sessionId, user = Context.UserIdentifier });
            await base.OnConnectedAsync();
        }
    }

    /// <summary>
    ///     Gets the correlation ID for the current connection.
    ///     First tries to get it from connection items (stored during OnConnectedAsync),
    ///     then from the HTTP context (set by CorrelationIdMiddleware during handshake),
    ///     and finally generates a new one if not found.
    /// </summary>
    private string GetCorrelationId()
    {
        // Try to get from connection items (stored during OnConnectedAsync)
        if (Context.Items.TryGetValue("CorrelationId", out var storedId) &&
            storedId is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        // Try to get from HTTP context (set by middleware during handshake)
        var httpContext = Context.GetHttpContext();
        if (httpContext != null &&
            httpContext.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var itemId) &&
            itemId is string existingId &&
            !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId;
        }

        // Generate a new one as fallback
        return Guid.NewGuid().ToString("N")[..12];
    }
}