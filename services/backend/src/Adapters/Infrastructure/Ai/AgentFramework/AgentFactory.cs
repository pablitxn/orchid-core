using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Infrastructure.Ai.AgentFramework;

/// <summary>
/// Factory for creating AI agents with configurable models.
/// Thread-safe: OpenAIClient is thread-safe, and this factory creates new instances per call.
/// </summary>
public sealed class AgentFactory : IAgentFactory
{
    private readonly OpenAIClient _openAIClient;
    private readonly string _defaultModel;

    public AgentFactory(OpenAIClient openAIClient, string defaultModel)
    {
        _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient));
        _defaultModel = defaultModel ?? throw new ArgumentNullException(nameof(defaultModel));
    }

    /// <inheritdoc />
    public AIAgent CreateAgent(
        string instructions,
        string? name = null,
        IReadOnlyList<AIFunction>? tools = null)
    {
        return CreateAgentWithModel(instructions, _defaultModel, name, tools);
    }

    /// <inheritdoc />
    public AIAgent CreateAgentWithModel(
        string instructions,
        string modelId,
        string? name = null,
        IReadOnlyList<AIFunction>? tools = null)
    {
        var chatClient = GetChatClient(modelId);

        // Convert AIFunction to AITool for the Agent Framework API
        var aiTools = tools?.Select(f => (AITool)f).ToList();

        return chatClient.CreateAIAgent(
            instructions: instructions,
            name: name,
            tools: aiTools);
    }

    /// <inheritdoc />
    public IChatClient GetChatClient(string? modelId = null)
    {
        var model = modelId ?? _defaultModel;
        return _openAIClient
            .GetChatClient(model)
            .AsIChatClient();
    }
}
