using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Infrastructure.Ai.SemanticKernel;

/// <summary>
///     Semantic Kernel adapter implementing IChatCompletionPort via SK's chat service with Langfuse telemetry.
/// </summary>
public sealed class SemanticKernelChatCompletionAdapter : IChatCompletionPort
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;
    private readonly IEnhancedTelemetryClient? _telemetry;
    private readonly ILogger<SemanticKernelChatCompletionAdapter>? _logger;
    private readonly string _modelId;
    private readonly IAgentPluginLoader? _agentPluginLoader;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance using the provided Semantic Kernel.
    /// </summary>
    public SemanticKernelChatCompletionAdapter(
        Kernel kernel,
        IServiceProvider serviceProvider,
        IAgentPluginLoader? agentPluginLoader = null,
        IEnhancedTelemetryClient? telemetry = null,
        ILogger<SemanticKernelChatCompletionAdapter>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _kernel = kernel;
        _serviceProvider = serviceProvider;
        _agentPluginLoader = agentPluginLoader;
        _telemetry = telemetry;
        _logger = logger;

        // Resolve the chat completion service
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();

        // Set a default model ID (can be overridden by configuration)
        _modelId = "gpt-4o";

        // Register telemetry filter if telemetry is available
        if (_telemetry != null)
        {
            // Try to resolve credit tracking service
            var creditTrackingService = _serviceProvider.GetService<ICreditTrackingService>();
            var pluginRepository = _serviceProvider.GetService<IPluginRepository>();
            kernel.FunctionInvocationFilters.Add(new TelemetryFunctionFilter(_telemetry, _logger, creditTrackingService,
                pluginRepository));
        }
    }

    public async Task<string> CompleteAsync(IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        // Convert to list once to avoid multiple enumeration
        var messagesList = messages.ToList();
        var sessionId = ExtractSessionId(messagesList);

        // Start telemetry trace if telemetry is available
        string? traceId = null;
        if (_telemetry != null && !string.IsNullOrEmpty(sessionId))
        {
            traceId = await _telemetry.StartTraceAsync(
                $"Chat Completion - {sessionId}",
                sessionId,
                metadata: new { adapter = "SemanticKernel", model = _modelId },
                cancellationToken: cancellationToken
            );
        }

        try
        {
            // Build chat history from provided messages
            var history = new ChatHistory();
            var inputMessages = new List<object>();

            foreach (var msg in messagesList)
            {
                var role = msg.Role.ToLowerInvariant();
                switch (role)
                {
                    case "assistant":
                        history.AddAssistantMessage(msg.Content);
                        inputMessages.Add(new { role = "assistant", content = msg.Content });
                        break;
                    case "user":
                        history.AddUserMessage(msg.Content);
                        inputMessages.Add(new { role = "user", content = msg.Content });
                        break;
                    case "system":
                        history.AddSystemMessage(msg.Content);
                        inputMessages.Add(new { role = "system", content = msg.Content });
                        break;
                    // Any other role (e.g. activity) is skipped
                }
            }

            // Record input messages in telemetry
            if (_telemetry != null && traceId != null)
            {
                await _telemetry.UpdateTraceAsync(traceId, input: new { messages = inputMessages },
                    cancellationToken: cancellationToken);
            }

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.4,
                MaxTokens = 4096
            };

            // Create a generation span for the LLM call
            string? generationId = null;
            if (_telemetry != null && traceId != null)
            {
                generationId = await _telemetry.RecordGenerationAsync(
                    traceId,
                    _modelId,
                    input: new
                    {
                        messages = inputMessages,
                        settings = new { temperature = settings.Temperature, maxTokens = settings.MaxTokens }
                    },
                    metadata: new { messageCount = history.Count, sessionId },
                    cancellationToken: cancellationToken
                );
            }

            // Store context for filters
            if (_telemetry != null && traceId != null)
            {
                _kernel.Data["telemetry_trace_id"] = traceId;
                _kernel.Data["telemetry_session_id"] = sessionId;
            }

            var result = await _chatService.GetChatMessageContentAsync(
                history,
                settings,
                _kernel,
                cancellationToken);

            var response = result.Content ?? string.Empty;

            // Record generation output
            if (_telemetry != null && traceId != null && generationId != null)
            {
                // Extract usage data if available
                var usage = result.Metadata?.TryGetValue("Usage", out var usageObj) == true ? usageObj : null;

                await _telemetry.RecordGenerationAsync(
                    traceId,
                    _modelId,
                    output: new
                    {
                        content = response,
                        finishReason = result.Metadata?.TryGetValue("FinishReason", out var fr) == true ? fr : null
                    },
                    metadata: new
                    {
                        success = true,
                        usage,
                        sessionId,
                        functionCalls = result.Metadata?.TryGetValue("FunctionCalls", out var fc) == true ? fc : null
                    },
                    cancellationToken: cancellationToken
                );
            }

            // Update trace with output
            if (_telemetry != null && traceId != null)
            {
                await _telemetry.UpdateTraceAsync(traceId, output: new { response },
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

            // Record error in telemetry
            if (_telemetry != null && traceId != null)
            {
                await _telemetry.RecordEventAsync(traceId, "error",
                    new { error = ex.Message, type = ex.GetType().Name, model = _modelId },
                    cancellationToken: cancellationToken);
                await _telemetry.EndTraceAsync(traceId, false, new { error = ex.Message },
                    cancellationToken: cancellationToken);
            }

            throw;
        }
        finally
        {
            // Clean up context
            if (_kernel.Data.ContainsKey("telemetry_trace_id"))
            {
                _kernel.Data.Remove("telemetry_trace_id");
            }

            if (_kernel.Data.ContainsKey("telemetry_session_id"))
            {
                _kernel.Data.Remove("telemetry_session_id");
            }
        }
    }

    /// <summary>
    /// Extracts session ID from chat messages, looking for metadata or patterns
    /// </summary>
    private static string? ExtractSessionId(IEnumerable<ChatMessage> messages)
    {
        // First, try to find a session ID in message metadata
        foreach (var msg in messages)
        {
            if (msg.Metadata?.TryGetValue("sessionId", out var sessionId) == true)
            {
                return sessionId.ToString();
            }
        }

        // If no explicit session ID, try to extract from message content patterns
        var firstUserMessage = messages.FirstOrDefault(m =>
            m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));

        if (firstUserMessage != null)
        {
            // Simple pattern matching for session references
            var match = Regex.Match(
                firstUserMessage.Content,
                @"session[:\s]+([a-zA-Z0-9\-_]+)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        // Generate a default session ID based on message hash if none found
        var messagesHash = string.Join("|", messages.Take(3).Select(m => m.Content)).GetHashCode();
        return $"auto-{Math.Abs(messagesHash)}";
    }

    public async Task<string> CompleteWithAgentAsync(
        IEnumerable<ChatMessage> messages,
        Guid agentId,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        // Create a scoped kernel for this specific agent completion
        var scopedKernel = await CreateAgentScopedKernelAsync(agentId, cancellationToken);

        // If userId is provided, store it in kernel data for credit tracking
        if (userId.HasValue)
        {
            scopedKernel.Data["user_id"] = userId.Value;
        }

        // Use the scoped kernel with agent-specific plugins
        var adapter = new SemanticKernelChatCompletionAdapter(
            scopedKernel,
            _serviceProvider,
            _agentPluginLoader,
            _telemetry,
            _logger);

        return await adapter.CompleteAsync(messages, cancellationToken);
    }

    /// <summary>
    /// Creates a kernel instance with agent-specific plugins loaded
    /// </summary>
    private async Task<Kernel> CreateAgentScopedKernelAsync(Guid agentId, CancellationToken cancellationToken)
    {
        if (_agentPluginLoader == null)
        {
            throw new InvalidOperationException(
                "Agent plugin loader is not configured. Cannot load agent-specific plugins.");
        }

        // Create a new kernel instance with the same services
        var scopedKernel = new Kernel(_serviceProvider);

        // Copy the chat completion service configuration
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        // Register telemetry filter if available
        if (_telemetry != null)
        {
            var creditTrackingService = _serviceProvider.GetService<ICreditTrackingService>();
            var pluginRepository = _serviceProvider.GetService<IPluginRepository>();
            scopedKernel.FunctionInvocationFilters.Add(new TelemetryFunctionFilter(_telemetry, _logger,
                creditTrackingService, pluginRepository));
        }

        // Load agent-specific plugins
        var loadedPlugins = await _agentPluginLoader.LoadAgentPluginsAsync(agentId, scopedKernel, cancellationToken);

        _logger?.LogInformation(
            "Created scoped kernel for agent {AgentId} with {PluginCount} plugins: {Plugins}",
            agentId,
            loadedPlugins.Count,
            string.Join(", ", loadedPlugins));

        return scopedKernel;
    }
}

/// <summary>
/// Function invocation filter for telemetry tracking using modern Semantic Kernel patterns
/// </summary>
internal sealed class TelemetryFunctionFilter : IFunctionInvocationFilter
{
    private readonly IEnhancedTelemetryClient _telemetry;
    private readonly ILogger? _logger;
    private readonly ICreditTrackingService? _creditTrackingService;
    private readonly IPluginRepository? _pluginRepository;

    public TelemetryFunctionFilter(
        IEnhancedTelemetryClient telemetry,
        ILogger? logger,
        ICreditTrackingService? creditTrackingService = null,
        IPluginRepository? pluginRepository = null)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _logger = logger;
        _creditTrackingService = creditTrackingService;
        _pluginRepository = pluginRepository;
    }

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        // Extract trace ID and session ID from kernel data
        var traceId = context.Kernel.Data.TryGetValue("telemetry_trace_id", out var tid) ? tid?.ToString() : null;
        var sessionId = context.Kernel.Data.TryGetValue("telemetry_session_id", out var sid) ? sid?.ToString() : null;

        if (string.IsNullOrEmpty(traceId))
        {
            // No active trace, just proceed
            await next(context);
            return;
        }

        var functionName = context.Function.Name;
        var pluginName = context.Function.PluginName;
        var fullName = string.IsNullOrEmpty(pluginName) ? functionName : $"{pluginName}.{functionName}";

        // Serialize arguments safely
        object? arguments = null;
        try
        {
            if (context.Arguments?.Count > 0)
            {
                var args = new Dictionary<string, object?>();
                foreach (var kvp in context.Arguments)
                {
                    args[kvp.Key] = kvp.Value;
                }

                arguments = args;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to serialize function arguments");
            arguments = "Failed to serialize arguments";
        }

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Log function invocation start
            await _telemetry.RecordEventAsync(
                traceId,
                "function_invoking",
                new
                {
                    function = fullName,
                    plugin = pluginName,
                    arguments,
                    sessionId
                }
            );

            // Execute the function
            await next(context);

            // Calculate duration
            var duration = DateTimeOffset.UtcNow - startTime;

            // Serialize result safely
            object? result = null;
            try
            {
                result = context.Result?.GetValue<object?>() ?? context.Result?.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to serialize function result");
                result = "Failed to serialize result";
            }

            // Record successful invocation
            await _telemetry.RecordToolInvocationAsync(
                traceId,
                fullName,
                arguments,
                result
                // metadata: new
                // {
                //     plugin = pluginName,
                //     duration = duration.TotalMilliseconds,
                //     success = true,
                //     sessionId
                // }
            );

            await _telemetry.RecordEventAsync(
                traceId,
                "function_invoked",
                new
                {
                    function = fullName,
                    plugin = pluginName,
                    result,
                    duration = duration.TotalMilliseconds,
                    sessionId
                }
            );

            // Track credit consumption for plugin usage
            if (_creditTrackingService != null && _pluginRepository != null && !string.IsNullOrEmpty(pluginName))
            {
                try
                {
                    // Extract user ID from kernel data if available
                    if (context.Kernel.Data.TryGetValue("user_id", out var userIdObj) &&
                        userIdObj is Guid userId)
                    {
                        // Find the plugin by system name
                        var plugins = await _pluginRepository.ListAsync(CancellationToken.None);
                        var plugin = plugins.FirstOrDefault(p => p.SystemName == pluginName);

                        if (plugin != null)
                        {
                            _logger?.LogInformation(
                                "Tracking credit consumption for plugin {PluginName} (ID: {PluginId}) for user {UserId}",
                                pluginName, plugin.Id, userId);

                            await _creditTrackingService.TrackPluginUsageAsync(
                                userId,
                                plugin.Id,
                                sessionId ?? "unknown",
                                1, null, CancellationToken.None
                            );
                        }
                        else
                        {
                            _logger?.LogWarning(
                                "Plugin {PluginName} not found in repository, skipping credit tracking",
                                pluginName);
                        }
                    }
                    else
                    {
                        _logger?.LogWarning(
                            "User ID not found in kernel data, skipping credit tracking for plugin {PluginName}",
                            pluginName);
                    }
                }
                catch (Exception ex)
                {
                    // Don't fail the function invocation if credit tracking fails
                    _logger?.LogError(ex,
                        "Failed to track credit consumption for plugin {PluginName}",
                        pluginName);
                }
            }
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;

            // Record failed invocation
            await _telemetry.RecordToolInvocationAsync(
                traceId,
                fullName,
                arguments,
                result: null
                // metadata: new
                // {
                //     plugin = pluginName,
                //     duration = duration.TotalMilliseconds,
                //     success = false,
                //     error = ex.Message,
                //     errorType = ex.GetType().Name,
                //     sessionId
                // }
            );

            await _telemetry.RecordEventAsync(
                traceId,
                "function_error",
                new
                {
                    function = fullName,
                    plugin = pluginName,
                    error = ex.Message,
                    errorType = ex.GetType().Name,
                    duration = duration.TotalMilliseconds,
                    sessionId
                }
            );

            _logger?.LogError(ex, "Function invocation failed: {FunctionName}", fullName);
            throw;
        }
    }
}