using System.Text.RegularExpressions;
using Application.Interfaces;
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

            return response;
        }
        catch (Exception ex)
        {
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

        // Fetch agent entity from database
        var agentEntity = await _agentRepository.GetByIdAsync(agentId, cancellationToken);
        if (agentEntity == null)
        {
            _logger?.LogWarning("Agent {AgentId} not found in database", agentId);
            throw new InvalidOperationException($"Agent with ID {agentId} not found.");
        }

        // Determine model: use agent's configured model or fall back to default
        var modelId = agentEntity.Model ?? _defaultModelId;

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

            return response;
        }
        catch (Exception ex)
        {
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
