namespace Application.Interfaces;

/// <summary>
///     Port for chat completion functionality provided by an AI service.
/// </summary>
public interface IChatCompletionPort
{
    /// <summary>
    ///     Completes the given conversation messages and returns the AI-generated response.
    /// </summary>
    /// <param name="messages">The conversation history, including the latest user message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completion result as a string.</returns>
    Task<string> CompleteAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Completes the given conversation messages with agent-specific plugins and returns the AI-generated response.
    /// </summary>
    /// <param name="messages">The conversation history, including the latest user message.</param>
    /// <param name="agentId">The ID of the agent whose plugins should be used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="userId">Optional user ID for credit tracking.</param>
    /// <returns>The completion result as a string.</returns>
    Task<string> CompleteWithAgentAsync(IEnumerable<ChatMessage> messages, Guid agentId,
        Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Streams the AI-generated response token by token for the given conversation messages.
    /// </summary>
    /// <param name="messages">The conversation history, including the latest user message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of response tokens/chunks.</returns>
    IAsyncEnumerable<string> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Streams the AI-generated response token by token with agent-specific plugins.
    /// </summary>
    /// <param name="messages">The conversation history, including the latest user message.</param>
    /// <param name="agentId">The ID of the agent whose plugins should be used.</param>
    /// <param name="userId">Optional user ID for credit tracking.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of response tokens/chunks.</returns>
    IAsyncEnumerable<string> CompleteWithAgentStreamingAsync(
        IEnumerable<ChatMessage> messages,
        Guid agentId,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}