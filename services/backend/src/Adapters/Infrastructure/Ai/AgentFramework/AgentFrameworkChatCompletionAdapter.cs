using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Infrastructure.Ai.AgentFramework.Middleware;
using Infrastructure.Telemetry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AppChatMessage = Application.Interfaces.ChatMessage;

namespace Infrastructure.Ai.AgentFramework;

/// <summary>
/// Agent Framework adapter implementing IChatCompletionPort with Langfuse telemetry.
/// </summary>
public sealed class AgentFrameworkChatCompletionAdapter : IChatCompletionPort
{
    private readonly IChatClient _chatClient;
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentRepository _agentRepository;
    private readonly IEnhancedTelemetryClient? _telemetry;
    private readonly ILogger<AgentFrameworkChatCompletionAdapter>? _logger;
    private readonly string _defaultModelId;
    private readonly IServiceProvider _serviceProvider;

    public AgentFrameworkChatCompletionAdapter(
        IChatClient chatClient,
        IAgentFactory agentFactory,
        IAgentRepository agentRepository,
        IServiceProvider serviceProvider,
        IEnhancedTelemetryClient? telemetry = null,
        ILogger<AgentFrameworkChatCompletionAdapter>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(agentFactory);
        ArgumentNullException.ThrowIfNull(agentRepository);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _chatClient = chatClient;
        _agentFactory = agentFactory;
        _agentRepository = agentRepository;
        _serviceProvider = serviceProvider;
        _telemetry = telemetry;
        _logger = logger;
        _defaultModelId = "gpt-5-nano-2025-08-07";
    }

    public async Task<string> CompleteAsync(
        IEnumerable<AppChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var messagesList = messages.ToList();
        var sessionId = ExtractSessionId(messagesList);
        var metricsStopwatch = Stopwatch.StartNew();

        string? traceId = null;
        if (_telemetry != null && !string.IsNullOrEmpty(sessionId))
        {
            traceId = await _telemetry.StartTraceAsync(
                $"Chat Completion - {sessionId}",
                sessionId,
                metadata: new { adapter = "AgentFramework", model = _defaultModelId },
                cancellationToken: cancellationToken);
        }

        try
        {
            var chatMessages = ConvertToChatMessages(messagesList);
            var inputMessages = messagesList
                .Where(m => m.Role is "user" or "assistant" or "system")
                .Select(m => new { role = m.Role, content = m.Content })
                .ToList();

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.UpdateTraceAsync(traceId,
                    input: new { messages = inputMessages },
                    cancellationToken: cancellationToken);
            }

            string? generationId = null;
            if (_telemetry != null && traceId != null)
            {
                generationId = await _telemetry.RecordGenerationAsync(
                    traceId,
                    _defaultModelId,
                    input: new { messages = inputMessages },
                    metadata: new { messageCount = chatMessages.Count, sessionId },
                    cancellationToken: cancellationToken);
            }

            var result = await _chatClient.GetResponseAsync(
                chatMessages,
                cancellationToken: cancellationToken);

            var response = result.Text ?? string.Empty;

            if (_telemetry != null && traceId != null && generationId != null)
            {
                await _telemetry.RecordGenerationAsync(
                    traceId,
                    _defaultModelId,
                    output: new { content = response },
                    metadata: new { success = true, sessionId },
                    cancellationToken: cancellationToken);
            }

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.UpdateTraceAsync(traceId,
                    output: new { response },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, true, cancellationToken: cancellationToken);
            }

            // Record Prometheus metrics for successful AI operation
            metricsStopwatch.Stop();
            ApplicationMetrics.AiOperationsTotal.WithLabels("chat_completion", _defaultModelId, "success").Inc();
            ApplicationMetrics.AiOperationDuration.WithLabels("chat_completion", _defaultModelId).Observe(metricsStopwatch.Elapsed.TotalSeconds);

            return response;
        }
        catch (Exception ex)
        {
            // Record Prometheus metrics for failed AI operation
            metricsStopwatch.Stop();
            ApplicationMetrics.AiOperationsTotal.WithLabels("chat_completion", _defaultModelId, "error").Inc();
            ApplicationMetrics.AiOperationDuration.WithLabels("chat_completion", _defaultModelId).Observe(metricsStopwatch.Elapsed.TotalSeconds);

            _logger?.LogError(ex,
                "Error in chat completion. Model: {Model}, MessageCount: {MessageCount}, Error: {ErrorMessage}",
                _defaultModelId, messagesList.Count, ex.Message);

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.RecordEventAsync(traceId, "error",
                    new { error = ex.Message, type = ex.GetType().Name, model = _defaultModelId },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, false,
                    new { error = ex.Message },
                    cancellationToken: cancellationToken);
            }

            throw;
        }
    }

    public async Task<string> CompleteWithAgentAsync(
        IEnumerable<AppChatMessage> messages,
        Guid agentId,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var messagesList = messages.ToList();
        var sessionId = ExtractSessionId(messagesList);
        var metricsStopwatch = Stopwatch.StartNew();
        var metricsModelId = _defaultModelId; // Will be updated once we know the agent's model

        // Fetch agent entity from database
        var agentEntity = await _agentRepository.GetByIdAsync(agentId, cancellationToken);
        if (agentEntity == null)
        {
            metricsStopwatch.Stop();
            ApplicationMetrics.AiOperationsTotal.WithLabels("agent_completion", metricsModelId, "error").Inc();
            _logger?.LogWarning("Agent {AgentId} not found in database", agentId);
            throw new InvalidOperationException($"Agent with ID {agentId} not found.");
        }

        // Determine model: use agent's configured model or fall back to default
        var modelId = agentEntity.Model ?? _defaultModelId;
        metricsModelId = modelId;

        string? traceId = null;
        if (_telemetry != null && !string.IsNullOrEmpty(sessionId))
        {
            traceId = await _telemetry.StartTraceAsync(
                $"Agent Completion - {agentId}",
                sessionId,
                userId: userId?.ToString(),
                metadata: new { adapter = "AgentFramework", model = modelId, agentId, agentName = agentEntity.Name },
                cancellationToken: cancellationToken);
        }

        // Establish telemetry context for tool invocations
        using var telemetryScope = !string.IsNullOrEmpty(traceId)
            ? TelemetryContext.BeginScope(traceId, sessionId, userId?.ToString())
            : TelemetryContext.BeginDisabledScope();

        try
        {
            // Load agent configuration and tools
            var toolLoader = _serviceProvider.GetService<IAgentToolLoader>();
            var tools = toolLoader != null
                ? await toolLoader.LoadAgentToolsAsync(agentId, cancellationToken)
                : Array.Empty<AIFunction>();

            // Build system message from agent personality if not provided
            var systemMessage = messagesList.FirstOrDefault(m =>
                m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

            // Prepare messages for chat completion
            var effectiveMessages = messagesList.ToList();
            if (systemMessage == null && !string.IsNullOrEmpty(agentEntity.Personality))
            {
                // Prepend personality as system message
                effectiveMessages.Insert(0, new AppChatMessage("system", agentEntity.Personality));
            }

            // Convert messages to chat format
            var chatMessages = ConvertToChatMessages(effectiveMessages);

            var inputMessages = messagesList
                .Where(m => m.Role is "user" or "assistant")
                .Select(m => new { role = m.Role, content = m.Content })
                .ToList();

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.UpdateTraceAsync(traceId,
                    input: new { messages = inputMessages, tools = tools.Select(t => t.Name).ToList() },
                    cancellationToken: cancellationToken);

                await _telemetry.RecordGenerationAsync(traceId, modelId,
                    input: new { messages = inputMessages },
                    metadata: new { agentId, agentName = agentEntity.Name, toolCount = tools.Count },
                    cancellationToken: cancellationToken);
            }

            // Get the chat client configured for this model
            var chatClient = _agentFactory.GetChatClient(modelId);

            // Configure chat options with tools
            var chatOptions = new ChatOptions
            {
                Tools = tools.Cast<AITool>().ToList()
            };

            // Run the completion with tool support
            var result = await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken);
            var response = result.Text ?? string.Empty;

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.RecordGenerationAsync(traceId, modelId,
                    output: new { content = response },
                    metadata: new { success = true },
                    cancellationToken: cancellationToken);

                await _telemetry.UpdateTraceAsync(traceId,
                    output: new { response },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, true, cancellationToken: cancellationToken);
            }

            _logger?.LogInformation(
                "Agent {AgentId} ({AgentName}) completed with {ToolCount} tools available using model {Model}",
                agentId, agentEntity.Name, tools.Count, modelId);

            // Record Prometheus metrics for successful AI operation
            metricsStopwatch.Stop();
            ApplicationMetrics.AiOperationsTotal.WithLabels("agent_completion", metricsModelId, "success").Inc();
            ApplicationMetrics.AiOperationDuration.WithLabels("agent_completion", metricsModelId).Observe(metricsStopwatch.Elapsed.TotalSeconds);

            return response;
        }
        catch (Exception ex)
        {
            // Record Prometheus metrics for failed AI operation
            metricsStopwatch.Stop();
            ApplicationMetrics.AiOperationsTotal.WithLabels("agent_completion", metricsModelId, "error").Inc();
            ApplicationMetrics.AiOperationDuration.WithLabels("agent_completion", metricsModelId).Observe(metricsStopwatch.Elapsed.TotalSeconds);

            _logger?.LogError(ex,
                "Error in agent completion. AgentId: {AgentId}, Model: {Model}, Error: {ErrorMessage}",
                agentId, modelId, ex.Message);

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.RecordEventAsync(traceId, "error",
                    new { error = ex.Message, type = ex.GetType().Name, agentId, model = modelId },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, false,
                    new { error = ex.Message },
                    cancellationToken: cancellationToken);
            }

            throw;
        }
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        IEnumerable<AppChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messagesList = messages.ToList();
        var sessionId = ExtractSessionId(messagesList);
        var metricsStopwatch = Stopwatch.StartNew();
        var metricsRecorded = false;

        string? traceId = null;
        if (_telemetry != null && !string.IsNullOrEmpty(sessionId))
        {
            traceId = await _telemetry.StartTraceAsync(
                $"Streaming Completion - {sessionId}",
                sessionId,
                metadata: new { adapter = "AgentFramework", model = _defaultModelId, streaming = true },
                cancellationToken: cancellationToken);
        }

        var fullResponse = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            var chatMessages = ConvertToChatMessages(messagesList);
            var inputMessages = messagesList
                .Where(m => m.Role is "user" or "assistant" or "system")
                .Select(m => new { role = m.Role, content = m.Content })
                .ToList();

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.UpdateTraceAsync(traceId,
                    input: new { messages = inputMessages },
                    cancellationToken: cancellationToken);

                await _telemetry.RecordGenerationAsync(traceId, _defaultModelId,
                    input: new { messages = inputMessages },
                    metadata: new { messageCount = chatMessages.Count, sessionId, streaming = true },
                    cancellationToken: cancellationToken);
            }

            await foreach (var update in _chatClient.GetStreamingResponseAsync(
                               chatMessages,
                               cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    fullResponse.Append(update.Text);
                    yield return update.Text;
                }
            }

            var duration = DateTime.UtcNow - startTime;

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.RecordGenerationAsync(traceId, _defaultModelId,
                    output: new { content = fullResponse.ToString() },
                    metadata: new { success = true, sessionId, streaming = true, durationMs = duration.TotalMilliseconds },
                    cancellationToken: cancellationToken);

                await _telemetry.UpdateTraceAsync(traceId,
                    output: new { response = fullResponse.ToString() },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, true, cancellationToken: cancellationToken);
            }

            // Record Prometheus metrics for successful streaming
            metricsStopwatch.Stop();
            ApplicationMetrics.AiOperationsTotal.WithLabels("streaming_completion", _defaultModelId, "success").Inc();
            ApplicationMetrics.AiOperationDuration.WithLabels("streaming_completion", _defaultModelId).Observe(metricsStopwatch.Elapsed.TotalSeconds);
            metricsRecorded = true;
        }
        finally
        {
            // Record metrics if not already recorded (interrupted/error case)
            if (!metricsRecorded)
            {
                metricsStopwatch.Stop();
                ApplicationMetrics.AiOperationsTotal.WithLabels("streaming_completion", _defaultModelId, "interrupted").Inc();
                ApplicationMetrics.AiOperationDuration.WithLabels("streaming_completion", _defaultModelId).Observe(metricsStopwatch.Elapsed.TotalSeconds);
            }

            // If we exit early (cancellation/error) without completing telemetry, clean up
            if (_telemetry != null && traceId != null && fullResponse.Length == 0)
            {
                await _telemetry.EndTraceAsync(traceId, false,
                    new { error = "Stream interrupted", partial = fullResponse.ToString() },
                    cancellationToken: CancellationToken.None);
            }
        }
    }

    public async IAsyncEnumerable<string> CompleteWithAgentStreamingAsync(
        IEnumerable<AppChatMessage> messages,
        Guid agentId,
        Guid? userId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messagesList = messages.ToList();
        var sessionId = ExtractSessionId(messagesList);
        var metricsStopwatch = Stopwatch.StartNew();
        var metricsModelId = _defaultModelId;
        var metricsRecorded = false;

        // Fetch agent entity from database
        var agentEntity = await _agentRepository.GetByIdAsync(agentId, cancellationToken);
        if (agentEntity == null)
        {
            metricsStopwatch.Stop();
            ApplicationMetrics.AiOperationsTotal.WithLabels("agent_streaming", metricsModelId, "error").Inc();
            _logger?.LogWarning("Agent {AgentId} not found in database", agentId);
            throw new InvalidOperationException($"Agent with ID {agentId} not found.");
        }

        // Determine model: use agent's configured model or fall back to default
        var modelId = agentEntity.Model ?? _defaultModelId;
        metricsModelId = modelId;

        string? traceId = null;
        if (_telemetry != null && !string.IsNullOrEmpty(sessionId))
        {
            traceId = await _telemetry.StartTraceAsync(
                $"Agent Streaming - {agentId}",
                sessionId,
                userId: userId?.ToString(),
                metadata: new { adapter = "AgentFramework", model = modelId, agentId, agentName = agentEntity.Name, streaming = true },
                cancellationToken: cancellationToken);
        }

        var fullResponse = new StringBuilder();
        var startTime = DateTime.UtcNow;

        // Establish telemetry context for tool invocations (scope is manually managed for streaming)
        var telemetryScope = !string.IsNullOrEmpty(traceId)
            ? TelemetryContext.BeginScope(traceId, sessionId, userId?.ToString())
            : TelemetryContext.BeginDisabledScope();

        try
        {
            // Load agent configuration and tools
            var toolLoader = _serviceProvider.GetService<IAgentToolLoader>();
            var tools = toolLoader != null
                ? await toolLoader.LoadAgentToolsAsync(agentId, cancellationToken)
                : Array.Empty<AIFunction>();

            // Build system message from agent personality if not provided
            var systemMessage = messagesList.FirstOrDefault(m =>
                m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

            // Prepare messages for chat completion
            var effectiveMessages = messagesList.ToList();
            if (systemMessage == null && !string.IsNullOrEmpty(agentEntity.Personality))
            {
                // Prepend personality as system message
                effectiveMessages.Insert(0, new AppChatMessage("system", agentEntity.Personality));
            }

            // Convert messages to chat format
            var chatMessages = ConvertToChatMessages(effectiveMessages);

            var inputMessages = messagesList
                .Where(m => m.Role is "user" or "assistant")
                .Select(m => new { role = m.Role, content = m.Content })
                .ToList();

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.UpdateTraceAsync(traceId,
                    input: new { messages = inputMessages, tools = tools.Select(t => t.Name).ToList() },
                    cancellationToken: cancellationToken);

                await _telemetry.RecordGenerationAsync(traceId, modelId,
                    input: new { messages = inputMessages },
                    metadata: new { agentId, agentName = agentEntity.Name, toolCount = tools.Count, streaming = true },
                    cancellationToken: cancellationToken);
            }

            // Get the chat client configured for this model
            var chatClient = _agentFactory.GetChatClient(modelId);

            // Configure chat options with tools
            var chatOptions = new ChatOptions
            {
                Tools = tools.Cast<AITool>().ToList()
            };

            // Stream the response
            await foreach (var update in chatClient.GetStreamingResponseAsync(
                               chatMessages,
                               chatOptions,
                               cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    fullResponse.Append(update.Text);
                    yield return update.Text;
                }
            }

            var duration = DateTime.UtcNow - startTime;

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.RecordGenerationAsync(traceId, modelId,
                    output: new { content = fullResponse.ToString() },
                    metadata: new { success = true, streaming = true, durationMs = duration.TotalMilliseconds },
                    cancellationToken: cancellationToken);

                await _telemetry.UpdateTraceAsync(traceId,
                    output: new { response = fullResponse.ToString() },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, true, cancellationToken: cancellationToken);
            }

            _logger?.LogInformation(
                "Agent {AgentId} ({AgentName}) streaming completed with {ToolCount} tools using model {Model} in {DurationMs}ms",
                agentId, agentEntity.Name, tools.Count, modelId, duration.TotalMilliseconds);

            // Record Prometheus metrics for successful agent streaming
            metricsStopwatch.Stop();
            ApplicationMetrics.AiOperationsTotal.WithLabels("agent_streaming", metricsModelId, "success").Inc();
            ApplicationMetrics.AiOperationDuration.WithLabels("agent_streaming", metricsModelId).Observe(metricsStopwatch.Elapsed.TotalSeconds);
            metricsRecorded = true;
        }
        finally
        {
            // Record metrics if not already recorded (interrupted/error case)
            if (!metricsRecorded)
            {
                metricsStopwatch.Stop();
                ApplicationMetrics.AiOperationsTotal.WithLabels("agent_streaming", metricsModelId, "interrupted").Inc();
                ApplicationMetrics.AiOperationDuration.WithLabels("agent_streaming", metricsModelId).Observe(metricsStopwatch.Elapsed.TotalSeconds);
            }

            // Dispose telemetry scope
            telemetryScope.Dispose();

            // If we exit early (cancellation/error) without completing telemetry, clean up
            if (_telemetry != null && traceId != null && fullResponse.Length == 0)
            {
                await _telemetry.EndTraceAsync(traceId, false,
                    new { error = "Stream interrupted", partial = fullResponse.ToString() },
                    cancellationToken: CancellationToken.None);
            }
        }
    }

    private static List<Microsoft.Extensions.AI.ChatMessage> ConvertToChatMessages(
        IEnumerable<AppChatMessage> messages)
    {
        var result = new List<Microsoft.Extensions.AI.ChatMessage>();

        foreach (var msg in messages)
        {
            var role = msg.Role.ToLowerInvariant() switch
            {
                "assistant" => ChatRole.Assistant,
                "user" => ChatRole.User,
                "system" => ChatRole.System,
                _ => ChatRole.User
            };

            result.Add(new Microsoft.Extensions.AI.ChatMessage(role, msg.Content));
        }

        return result;
    }

    private static string? ExtractSessionId(IEnumerable<AppChatMessage> messages)
    {
        foreach (var msg in messages)
        {
            if (msg.Metadata?.TryGetValue("sessionId", out var sessionId) == true)
            {
                return sessionId.ToString();
            }
        }

        var firstUserMessage = messages.FirstOrDefault(m =>
            m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));

        if (firstUserMessage != null)
        {
            var match = Regex.Match(
                firstUserMessage.Content,
                @"session[:\s]+([a-zA-Z0-9\-_]+)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        var messagesHash = string.Join("|", messages.Take(3).Select(m => m.Content)).GetHashCode();
        return $"auto-{Math.Abs(messagesHash)}";
    }
}
