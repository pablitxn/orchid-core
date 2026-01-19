using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.AgentFramework.Abstractions;

/// <summary>
/// Represents a tool that can be used by AI agents.
/// Tools provide specific capabilities like vector search or web search.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Internal system name used for tool identification (e.g., "vector_store", "web_search").
    /// Must match the SystemName in the Plugins database table.
    /// </summary>
    string SystemName { get; }

    /// <summary>
    /// Human-readable display name for UI presentation (e.g., "Vector Store", "Web Search").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Description of what this tool does, shown in UI.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Cost in credits per tool invocation.
    /// </summary>
    int CreditCost { get; }

    /// <summary>
    /// Whether this tool requires an active subscription to use.
    /// </summary>
    bool RequiresSubscription { get; }

    /// <summary>
    /// Gets the AIFunction instances that this tool provides.
    /// </summary>
    /// <returns>List of AIFunction tools for use with Agent Framework.</returns>
    IReadOnlyList<AIFunction> GetAIFunctions();
}
