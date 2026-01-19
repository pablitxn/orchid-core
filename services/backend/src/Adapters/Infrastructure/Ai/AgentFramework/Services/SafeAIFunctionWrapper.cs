using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Wraps an AIFunction with error handling to prevent tool failures from crashing conversations.
/// When the wrapped function throws an exception, this wrapper catches it, logs the error,
/// and returns an error message to the LLM instead of propagating the exception.
/// </summary>
public sealed class SafeAIFunctionWrapper : AIFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly AIFunction _innerFunction;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a new SafeAIFunctionWrapper that wraps the specified function with error handling.
    /// </summary>
    /// <param name="innerFunction">The function to wrap.</param>
    /// <param name="logger">Logger for error reporting.</param>
    public SafeAIFunctionWrapper(AIFunction innerFunction, ILogger logger)
    {
        _innerFunction = innerFunction ?? throw new ArgumentNullException(nameof(innerFunction));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    /// Invokes the wrapped function with error handling.
    /// If the function throws an exception, logs the error and returns an error message to the LLM.
    /// </summary>
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _innerFunction.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation should propagate - the user requested to stop
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Tool '{ToolName}' failed during invocation. Error: {ErrorMessage}",
                _innerFunction.Name,
                ex.Message);

            // Return an error message that the LLM can understand and respond to appropriately
            var errorResponse = new ToolErrorResponse(
                Success: false,
                Error: ex.GetType().Name,
                Message: $"The tool '{_innerFunction.Name}' encountered an error: {ex.Message}",
                Recoverable: IsRecoverableError(ex));

            return JsonSerializer.Serialize(errorResponse, JsonOptions);
        }
    }

    /// <summary>
    /// Determines if an error is potentially recoverable (e.g., temporary network issues).
    /// </summary>
    private static bool IsRecoverableError(Exception ex)
    {
        return ex is HttpRequestException
            or TimeoutException
            or TaskCanceledException { InnerException: TimeoutException };
    }

    /// <summary>
    /// Wraps a collection of AIFunctions with error handling.
    /// </summary>
    /// <param name="functions">The functions to wrap.</param>
    /// <param name="logger">Logger for error reporting.</param>
    /// <returns>Collection of wrapped functions.</returns>
    public static IReadOnlyList<AIFunction> WrapAll(
        IEnumerable<AIFunction> functions,
        ILogger logger)
    {
        return functions.Select(f => new SafeAIFunctionWrapper(f, logger)).ToList<AIFunction>();
    }

    /// <summary>
    /// Response structure for tool errors that the LLM can interpret.
    /// </summary>
    private sealed record ToolErrorResponse(
        bool Success,
        string Error,
        string Message,
        bool Recoverable);
}
