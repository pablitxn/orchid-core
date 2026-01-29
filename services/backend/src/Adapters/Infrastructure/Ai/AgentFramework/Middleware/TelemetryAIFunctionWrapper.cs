using System.Diagnostics;
using System.Text.Json;
using Application.Interfaces;
using Infrastructure.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Middleware;

/// <summary>
/// Wraps an AIFunction with telemetry tracking to record all tool invocations.
/// Records function_invoking, function_invoked, and function_error events.
/// Uses IEnhancedTelemetryClient for provider-agnostic telemetry recording.
/// Optionally tracks credit consumption for tool usage.
/// </summary>
public sealed class TelemetryAIFunctionWrapper : AIFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AIFunction _innerFunction;
    private readonly IEnhancedTelemetryClient _telemetryClient;
    private readonly ILogger _logger;
    private readonly ICreditTrackingService? _creditTrackingService;
    private readonly IPluginRepository? _pluginRepository;
    private readonly IPluginUsageTracker? _pluginUsageTracker;
    private readonly string? _toolSystemName;

    /// <summary>
    /// Creates a new TelemetryAIFunctionWrapper that wraps the specified function with telemetry tracking.
    /// </summary>
    /// <param name="innerFunction">The function to wrap.</param>
    /// <param name="telemetryClient">The telemetry client for recording events.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="creditTrackingService">Optional credit tracking service for billing.</param>
    /// <param name="pluginRepository">Optional plugin repository for resolving plugin IDs.</param>
    /// <param name="pluginUsageTracker">Optional plugin usage tracker for analytics.</param>
    /// <param name="toolSystemName">Optional tool system name for credit tracking.</param>
    public TelemetryAIFunctionWrapper(
        AIFunction innerFunction,
        IEnhancedTelemetryClient telemetryClient,
        ILogger logger,
        ICreditTrackingService? creditTrackingService = null,
        IPluginRepository? pluginRepository = null,
        IPluginUsageTracker? pluginUsageTracker = null,
        string? toolSystemName = null)
    {
        _innerFunction = innerFunction ?? throw new ArgumentNullException(nameof(innerFunction));
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _creditTrackingService = creditTrackingService;
        _pluginRepository = pluginRepository;
        _pluginUsageTracker = pluginUsageTracker;
        _toolSystemName = toolSystemName;
    }

    /// <inheritdoc />
    public override string Name => _innerFunction.Name;

    /// <inheritdoc />
    public override string Description => _innerFunction.Description;

    /// <inheritdoc />
    public override JsonElement JsonSchema => _innerFunction.JsonSchema;

    /// <inheritdoc />
    public override JsonElement? ReturnJsonSchema => _innerFunction.ReturnJsonSchema;

    /// <inheritdoc />
    public override JsonSerializerOptions? JsonSerializerOptions => _innerFunction.JsonSerializerOptions;

    /// <summary>
    /// Invokes the wrapped function with telemetry tracking.
    /// Records function_invoking before execution, and function_invoked/function_error after.
    /// </summary>
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var traceId = TelemetryContext.TraceId;
        var functionName = _innerFunction.Name;

        // Skip telemetry if no trace context is available (but still record Prometheus metrics)
        if (string.IsNullOrEmpty(traceId) || !TelemetryContext.IsEnabled)
        {
            _logger.LogDebug(
                "Telemetry skipped for function '{FunctionName}': no trace context available",
                functionName);

            var noContextStopwatch = Stopwatch.StartNew();
            try
            {
                var noContextResult = await _innerFunction.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
                noContextStopwatch.Stop();
                ApplicationMetrics.ToolInvocationsTotal.WithLabels(functionName, "success").Inc();
                ApplicationMetrics.ToolExecutionDuration.WithLabels(functionName).Observe(noContextStopwatch.Elapsed.TotalSeconds);
                return noContextResult;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                noContextStopwatch.Stop();
                ApplicationMetrics.ToolInvocationsTotal.WithLabels(functionName, "error").Inc();
                ApplicationMetrics.ToolExecutionDuration.WithLabels(functionName).Observe(noContextStopwatch.Elapsed.TotalSeconds);
                throw;
            }
        }

        var argumentsData = ExtractArgumentsData(arguments);
        var stopwatch = Stopwatch.StartNew();

        // Record function_invoking event BEFORE execution
        await RecordFunctionInvokingAsync(traceId, functionName, argumentsData, cancellationToken);

        try
        {
            var result = await _innerFunction.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            // Record function_invoked event AFTER successful execution
            await RecordFunctionInvokedAsync(
                traceId,
                functionName,
                argumentsData,
                result,
                stopwatch.ElapsedMilliseconds,
                cancellationToken);

            // Record Prometheus metrics for successful tool invocation
            ApplicationMetrics.ToolInvocationsTotal.WithLabels(functionName, "success").Inc();
            ApplicationMetrics.ToolExecutionDuration.WithLabels(functionName).Observe(stopwatch.Elapsed.TotalSeconds);

            // Track credit usage for successful tool invocations
            await TrackCreditUsageAsync(functionName, cancellationToken);

            // Track plugin usage for analytics (non-blocking)
            _ = TrackPluginInvocationAsync(functionName, stopwatch.ElapsedMilliseconds, cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            // Cancellation is not an error - just rethrow without recording error event
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record Prometheus metrics for failed tool invocation
            ApplicationMetrics.ToolInvocationsTotal.WithLabels(functionName, "error").Inc();
            ApplicationMetrics.ToolExecutionDuration.WithLabels(functionName).Observe(stopwatch.Elapsed.TotalSeconds);

            // Record function_error event
            await RecordFunctionErrorAsync(
                traceId,
                functionName,
                argumentsData,
                ex,
                stopwatch.ElapsedMilliseconds,
                cancellationToken);

            // Track plugin error for analytics (non-blocking)
            _ = TrackPluginErrorAsync(functionName, ex, cancellationToken);

            throw;
        }
    }

    private async Task RecordFunctionInvokingAsync(
        string traceId,
        string functionName,
        object? arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            await _telemetryClient.RecordEventAsync(
                traceId,
                "function_invoking",
                new
                {
                    function = functionName,
                    arguments,
                    timestamp = DateTimeOffset.UtcNow,
                    sessionId = TelemetryContext.SessionId,
                    userId = TelemetryContext.UserId
                },
                cancellationToken);

            _logger.LogDebug(
                "Recorded function_invoking event for '{FunctionName}' in trace {TraceId}",
                functionName,
                traceId);
        }
        catch (Exception ex)
        {
            // Telemetry failures should not break the function execution
            _logger.LogWarning(
                ex,
                "Failed to record function_invoking event for '{FunctionName}'",
                functionName);
        }
    }

    private async Task RecordFunctionInvokedAsync(
        string traceId,
        string functionName,
        object? arguments,
        object? result,
        long durationMs,
        CancellationToken cancellationToken)
    {
        try
        {
            // Record detailed tool invocation (creates a span in Langfuse)
            await _telemetryClient.RecordToolInvocationAsync(
                traceId,
                functionName,
                arguments,
                SerializeResult(result),
                cancellationToken);

            // Also record a discrete event for function_invoked
            await _telemetryClient.RecordEventAsync(
                traceId,
                "function_invoked",
                new
                {
                    function = functionName,
                    durationMs,
                    success = true,
                    resultType = result?.GetType().Name ?? "null",
                    timestamp = DateTimeOffset.UtcNow,
                    sessionId = TelemetryContext.SessionId
                },
                cancellationToken);

            _logger.LogDebug(
                "Recorded function_invoked event for '{FunctionName}' in trace {TraceId} ({DurationMs}ms)",
                functionName,
                traceId,
                durationMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to record function_invoked event for '{FunctionName}'",
                functionName);
        }
    }

    private async Task RecordFunctionErrorAsync(
        string traceId,
        string functionName,
        object? arguments,
        Exception exception,
        long durationMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await _telemetryClient.RecordEventAsync(
                traceId,
                "function_error",
                new
                {
                    function = functionName,
                    arguments,
                    error = exception.Message,
                    errorType = exception.GetType().Name,
                    stackTrace = exception.StackTrace,
                    durationMs,
                    timestamp = DateTimeOffset.UtcNow,
                    sessionId = TelemetryContext.SessionId,
                    userId = TelemetryContext.UserId
                },
                cancellationToken);

            _logger.LogDebug(
                "Recorded function_error event for '{FunctionName}' in trace {TraceId}: {Error}",
                functionName,
                traceId,
                exception.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to record function_error event for '{FunctionName}'",
                functionName);
        }
    }

    private static object? ExtractArgumentsData(AIFunctionArguments arguments)
    {
        try
        {
            // Convert AIFunctionArguments to a dictionary for serialization
            var dict = new Dictionary<string, object?>();
            foreach (var kvp in arguments)
            {
                dict[kvp.Key] = kvp.Value;
            }
            return dict.Count > 0 ? dict : null;
        }
        catch
        {
            return null;
        }
    }

    private static object? SerializeResult(object? result)
    {
        if (result == null) return null;

        try
        {
            // If result is already a string (JSON), return as-is
            if (result is string strResult)
            {
                return strResult;
            }

            // Serialize complex objects to JSON string
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch
        {
            return result.ToString();
        }
    }

    /// <summary>
    /// Tracks credit usage for a successful tool invocation.
    /// Only tracks if credit tracking is enabled and userId is available.
    /// </summary>
    private async Task TrackCreditUsageAsync(string functionName, CancellationToken cancellationToken)
    {
        // Skip if credit tracking is not configured
        if (_creditTrackingService == null || _pluginRepository == null)
        {
            return;
        }

        // Get userId from TelemetryContext
        var userIdString = TelemetryContext.UserId;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            _logger.LogDebug(
                "Credit tracking skipped for '{FunctionName}': no valid userId in context",
                functionName);
            return;
        }

        // Determine tool system name: use explicit value or lookup from context
        var toolSystemName = _toolSystemName ?? TelemetryContext.GetToolSystemName(functionName);
        if (string.IsNullOrEmpty(toolSystemName))
        {
            _logger.LogDebug(
                "Credit tracking skipped for '{FunctionName}': no tool system name available",
                functionName);
            return;
        }

        try
        {
            // Get plugin from repository to obtain the plugin ID and credit cost
            var plugin = await _pluginRepository.GetBySystemNameAsync(toolSystemName, cancellationToken);
            if (plugin == null)
            {
                _logger.LogWarning(
                    "Credit tracking skipped for '{FunctionName}': plugin '{ToolSystemName}' not found in database",
                    functionName,
                    toolSystemName);
                return;
            }

            var sessionId = TelemetryContext.SessionId ?? "unknown";
            var metadata = JsonSerializer.Serialize(new
            {
                functionName,
                sessionId,
                timestamp = DateTimeOffset.UtcNow
            }, JsonOptions);

            await _creditTrackingService.TrackPluginUsageAsync(
                userId,
                plugin.Id,
                plugin.Name,
                plugin.PriceCredits,
                metadata,
                cancellationToken);

            _logger.LogDebug(
                "Tracked credit usage for '{FunctionName}' (tool: {ToolSystemName}, credits: {Credits}, user: {UserId})",
                functionName,
                toolSystemName,
                plugin.PriceCredits,
                userId);
        }
        catch (Exception ex)
        {
            // Credit tracking failures should not break the function execution
            _logger.LogWarning(
                ex,
                "Failed to track credit usage for '{FunctionName}' (tool: {ToolSystemName})",
                functionName,
                toolSystemName);
        }
    }

    /// <summary>
    /// Tracks plugin invocation for analytics.
    /// Only tracks if usage tracker is configured and userId is available.
    /// </summary>
    private async Task TrackPluginInvocationAsync(string functionName, long durationMs, CancellationToken cancellationToken)
    {
        if (_pluginUsageTracker == null || _pluginRepository == null)
        {
            return;
        }

        var userIdString = TelemetryContext.UserId;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return;
        }

        var toolSystemName = _toolSystemName ?? TelemetryContext.GetToolSystemName(functionName);
        if (string.IsNullOrEmpty(toolSystemName))
        {
            return;
        }

        try
        {
            var plugin = await _pluginRepository.GetBySystemNameAsync(toolSystemName, cancellationToken);
            if (plugin == null)
            {
                return;
            }

            Guid? agentId = null;
            var agentIdString = TelemetryContext.AgentId;
            if (!string.IsNullOrEmpty(agentIdString) && Guid.TryParse(agentIdString, out var parsedAgentId))
            {
                agentId = parsedAgentId;
            }

            await _pluginUsageTracker.TrackInvocationAsync(
                plugin.Id,
                userId,
                agentId,
                plugin.Name,
                functionName,
                durationMs,
                cancellationToken);

            _logger.LogDebug(
                "Tracked plugin invocation for '{FunctionName}' (plugin: {PluginName}, user: {UserId}, duration: {DurationMs}ms)",
                functionName,
                plugin.Name,
                userId,
                durationMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to track plugin invocation for '{FunctionName}'",
                functionName);
        }
    }

    /// <summary>
    /// Tracks plugin error for analytics.
    /// Only tracks if usage tracker is configured and userId is available.
    /// </summary>
    private async Task TrackPluginErrorAsync(string functionName, Exception exception, CancellationToken cancellationToken)
    {
        if (_pluginUsageTracker == null || _pluginRepository == null)
        {
            return;
        }

        var userIdString = TelemetryContext.UserId;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return;
        }

        var toolSystemName = _toolSystemName ?? TelemetryContext.GetToolSystemName(functionName);
        if (string.IsNullOrEmpty(toolSystemName))
        {
            return;
        }

        try
        {
            var plugin = await _pluginRepository.GetBySystemNameAsync(toolSystemName, cancellationToken);
            if (plugin == null)
            {
                return;
            }

            Guid? agentId = null;
            var agentIdString = TelemetryContext.AgentId;
            if (!string.IsNullOrEmpty(agentIdString) && Guid.TryParse(agentIdString, out var parsedAgentId))
            {
                agentId = parsedAgentId;
            }

            await _pluginUsageTracker.TrackErrorAsync(
                plugin.Id,
                userId,
                agentId,
                plugin.Name,
                functionName,
                exception.Message,
                exception.GetType().Name,
                cancellationToken);

            _logger.LogDebug(
                "Tracked plugin error for '{FunctionName}' (plugin: {PluginName}, error: {ErrorType})",
                functionName,
                plugin.Name,
                exception.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to track plugin error for '{FunctionName}'",
                functionName);
        }
    }

    /// <summary>
    /// Wraps a collection of AIFunctions with telemetry tracking.
    /// </summary>
    /// <param name="functions">The functions to wrap.</param>
    /// <param name="telemetryClient">The telemetry client for recording events.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>Collection of wrapped functions.</returns>
    public static IReadOnlyList<AIFunction> WrapAll(
        IEnumerable<AIFunction> functions,
        IEnhancedTelemetryClient telemetryClient,
        ILogger logger)
    {
        return functions
            .Select(f => new TelemetryAIFunctionWrapper(f, telemetryClient, logger))
            .ToList<AIFunction>();
    }

    /// <summary>
    /// Wraps a collection of AIFunctions with telemetry tracking and credit tracking.
    /// </summary>
    /// <param name="functions">The functions to wrap.</param>
    /// <param name="telemetryClient">The telemetry client for recording events.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="creditTrackingService">Credit tracking service for billing.</param>
    /// <param name="pluginRepository">Plugin repository for resolving plugin IDs.</param>
    /// <param name="toolSystemName">The tool system name that all functions belong to.</param>
    /// <returns>Collection of wrapped functions with credit tracking.</returns>
    public static IReadOnlyList<AIFunction> WrapAllWithCreditTracking(
        IEnumerable<AIFunction> functions,
        IEnhancedTelemetryClient telemetryClient,
        ILogger logger,
        ICreditTrackingService creditTrackingService,
        IPluginRepository pluginRepository,
        string toolSystemName)
    {
        return functions
            .Select(f => new TelemetryAIFunctionWrapper(
                f,
                telemetryClient,
                logger,
                creditTrackingService,
                pluginRepository,
                null,
                toolSystemName))
            .ToList<AIFunction>();
    }

    /// <summary>
    /// Wraps a collection of AIFunctions with telemetry, credit tracking, and usage analytics.
    /// </summary>
    /// <param name="functions">The functions to wrap.</param>
    /// <param name="telemetryClient">The telemetry client for recording events.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="creditTrackingService">Credit tracking service for billing.</param>
    /// <param name="pluginRepository">Plugin repository for resolving plugin IDs.</param>
    /// <param name="pluginUsageTracker">Plugin usage tracker for analytics.</param>
    /// <param name="toolSystemName">The tool system name that all functions belong to.</param>
    /// <returns>Collection of wrapped functions with credit and usage tracking.</returns>
    public static IReadOnlyList<AIFunction> WrapAllWithFullTracking(
        IEnumerable<AIFunction> functions,
        IEnhancedTelemetryClient telemetryClient,
        ILogger logger,
        ICreditTrackingService creditTrackingService,
        IPluginRepository pluginRepository,
        IPluginUsageTracker pluginUsageTracker,
        string toolSystemName)
    {
        return functions
            .Select(f => new TelemetryAIFunctionWrapper(
                f,
                telemetryClient,
                logger,
                creditTrackingService,
                pluginRepository,
                pluginUsageTracker,
                toolSystemName))
            .ToList<AIFunction>();
    }
}
