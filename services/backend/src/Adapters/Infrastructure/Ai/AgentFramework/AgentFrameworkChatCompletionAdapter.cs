using System.Text.RegularExpressions;
using Application.Interfaces;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework;

/// <summary>
/// Agent Framework adapter implementing IChatCompletionPort with Langfuse telemetry.
/// </summary>
public sealed class AgentFrameworkChatCompletionAdapter : IChatCompletionPort
{
    private readonly IChatClient _chatClient;
    private readonly IAgentFactory _agentFactory;
    private readonly IEnhancedTelemetryClient? _telemetry;
    private readonly ILogger<AgentFrameworkChatCompletionAdapter>? _logger;
    private readonly string _modelId;
    private readonly IServiceProvider _serviceProvider;

    public AgentFrameworkChatCompletionAdapter(
        IChatClient chatClient,
        IAgentFactory agentFactory,
        IServiceProvider serviceProvider,
        IEnhancedTelemetryClient? telemetry = null,
        ILogger<AgentFrameworkChatCompletionAdapter>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(agentFactory);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _chatClient = chatClient;
        _agentFactory = agentFactory;
        _serviceProvider = serviceProvider;
        _telemetry = telemetry;
        _logger = logger;
        _modelId = "gpt-5-nano-2025-08-07";
    }

    public async Task<string> CompleteAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var messagesList = messages.ToList();
        var sessionId = ExtractSessionId(messagesList);

        string? traceId = null;
        if (_telemetry != null && !string.IsNullOrEmpty(sessionId))
        {
            traceId = await _telemetry.StartTraceAsync(
                $"Chat Completion - {sessionId}",
                sessionId,
                metadata: new { adapter = "AgentFramework", model = _modelId },
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
                    _modelId,
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
                    _modelId,
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

            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error in chat completion. Model: {Model}, MessageCount: {MessageCount}, Error: {ErrorMessage}",
                _modelId, messagesList.Count, ex.Message);

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.RecordEventAsync(traceId, "error",
                    new { error = ex.Message, type = ex.GetType().Name, model = _modelId },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, false,
                    new { error = ex.Message },
                    cancellationToken: cancellationToken);
            }

            throw;
        }
    }

    public async Task<string> CompleteWithAgentAsync(
        IEnumerable<ChatMessage> messages,
        Guid agentId,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var messagesList = messages.ToList();
        var sessionId = ExtractSessionId(messagesList);

        string? traceId = null;
        if (_telemetry != null && !string.IsNullOrEmpty(sessionId))
        {
            traceId = await _telemetry.StartTraceAsync(
                $"Agent Completion - {agentId}",
                sessionId,
                metadata: new { adapter = "AgentFramework", model = _modelId, agentId },
                cancellationToken: cancellationToken);
        }

        try
        {
            // Load agent configuration and tools
            var toolLoader = _serviceProvider.GetService<IAgentToolLoader>();
            var tools = toolLoader != null
                ? await toolLoader.LoadAgentToolsAsync(agentId, cancellationToken)
                : Array.Empty<AIFunction>();

            // Extract system message for agent instructions
            var systemMessage = messagesList.FirstOrDefault(m =>
                m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            var instructions = systemMessage?.Content ?? "You are a helpful assistant.";

            // Create agent with tools
            var agent = _agentFactory.CreateAgent(
                instructions: instructions,
                name: $"Agent-{agentId}",
                tools: tools.ToList());

            // Convert messages to chat format (excluding system message since it's in instructions)
            var chatMessages = ConvertToChatMessages(
                messagesList.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)));

            var inputMessages = messagesList
                .Where(m => m.Role is "user" or "assistant")
                .Select(m => new { role = m.Role, content = m.Content })
                .ToList();

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.UpdateTraceAsync(traceId,
                    input: new { messages = inputMessages, tools = tools.Select(t => t.Name).ToList() },
                    cancellationToken: cancellationToken);
            }

            // Run the agent
            var response = await agent.RunAsync(chatMessages, cancellationToken);

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.UpdateTraceAsync(traceId,
                    output: new { response },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, true, cancellationToken: cancellationToken);
            }

            _logger?.LogInformation(
                "Agent {AgentId} completed with {ToolCount} tools available",
                agentId, tools.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error in agent completion. AgentId: {AgentId}, Model: {Model}, Error: {ErrorMessage}",
                agentId, _modelId, ex.Message);

            if (_telemetry != null && traceId != null)
            {
                await _telemetry.RecordEventAsync(traceId, "error",
                    new { error = ex.Message, type = ex.GetType().Name, agentId },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, false,
                    new { error = ex.Message },
                    cancellationToken: cancellationToken);
            }

            throw;
        }
    }

    private static List<Microsoft.Extensions.AI.ChatMessage> ConvertToChatMessages(
        IEnumerable<ChatMessage> messages)
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

    private static string? ExtractSessionId(IEnumerable<ChatMessage> messages)
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
