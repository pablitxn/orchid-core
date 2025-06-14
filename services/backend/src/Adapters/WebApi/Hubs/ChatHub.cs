using System.Security.Claims;
using Application.Interfaces;
using Application.UseCases.Agent.VerifyAgentAccess;
using Application.UseCases.Subscription.ConsumeCredits;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using WebApi.Configuration;

namespace WebApi.Hubs;

public sealed class ChatHub(
    IChatCompletionPort chatCompletion,
    ICacheStore cache,
    IMediator mediator,
    ITokenCounter tokenCounter,
    IChatSessionRepository chatSessionRepository,
    IStringLocalizer<ChatHub> localizer,
    IOptions<CreditSystemConfiguration> creditConfig,
    IDistributedLockService distributedLock,
    IRateLimitService rateLimitService) : Hub
{
    private readonly ICacheStore _cache =
        cache ?? throw new ArgumentNullException(nameof(cache));

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
    public async Task SendMessage(string sessionId, string message, bool byTokens = false)
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
            await _mediator.Send(new ConsumeCreditsCommand(userId, credits), Context.ConnectionAborted);
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

        // Acquire distributed lock for this session to prevent race conditions
        var lockKey = $"chat_session:{sessionId}";
        await using var sessionLock = await _distributedLock.WaitAsync(
            lockKey, 
            TimeSpan.FromSeconds(30), 
            TimeSpan.FromSeconds(10), 
            Context.ConnectionAborted);

        // Key for storing conversation history, separated by session ID
        var key = $"chat_history:{sessionId}";
        // Retrieve existing history from cache

        var history = await _cache.GetAsync<List<ChatMessage>>(key) ?? [];

        // Append the new user message
        history.Add(new ChatMessage("user", message));

        // Broadcast the user's message as an activity and persist it
        await BroadcastActivity(sessionId, "user_message", new { sessionId, message });

        // Filter out activity messages before sending to LLM
        var llmHistory = history.Where(m => m.Role is "user" or "assistant" or "system").ToList();

        // Get the chat session to retrieve the agent ID
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

        if (!chatSession.AgentId.HasValue)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["NoAgentSelected"]);
            return;
        }

        // Verify user has access to the agent and its plugins
        var accessResult = await _mediator.Send(
            new VerifyAgentAccessQuery(userId, chatSession.AgentId.Value),
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

        // Invoke the AI completion port with filtered history and agent ID
        string response;
        try
        {
            response = await _chatCompletion.CompleteWithAgentAsync(llmHistory, chatSession.AgentId.Value, userId, Context.ConnectionAborted);
        }
        catch (TaskCanceledException)
        {
            response = _localizer["AiTimeout"];
        }
        catch (Exception ex)
        {
            response = string.Format(_localizer["AiError"], ex.Message);
        }

        // Append AI response to history
        history.Add(new ChatMessage("assistant", response));

        // Store updated history with a sliding expiration
        await _cache.SetAsync(key, history, TimeSpan.FromDays(1));

        // Send the AI response back to the caller
        await Clients.Caller.SendAsync("ReceiveMessage", "bot", response);
        // Broadcast the AI completion as an activity
        await BroadcastActivity(sessionId, "assistant_message", new { sessionId, message = response });
    }

    /// <summary>Returns the cached conversation for the given session.</summary>
    public async Task<IReadOnlyList<ChatMessage>> GetHistory(string sessionId)
    {
        var chatKey = $"chat_history:{sessionId}";
        var activityKey = $"activity_history:{sessionId}";

        var chatHistory = await _cache.GetAsync<List<ChatMessage>>(chatKey) ?? new List<ChatMessage>();
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

    public override async Task OnConnectedAsync()
    {
        var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString() ?? "unknown";
        // Send a welcome message to the connecting user
        await Clients.Caller.SendAsync("ReceiveMessage", "bot", _localizer["Welcome"]);
        // Broadcast an activity to all users
        await BroadcastActivity(sessionId, "user_connected", new { sessionId, user = Context.UserIdentifier });
        await base.OnConnectedAsync();
    }
}