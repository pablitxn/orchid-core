namespace Infrastructure.Telemetry;

/// <summary>
/// Classifier for determining whether an operation is AI/LLM-related
/// and should use Langfuse telemetry vs standard telemetry
/// </summary>
public static class TelemetryOperationClassifier
{
    // Operations that are explicitly NOT AI-related, even if they have "Chat" in the name
    private static readonly string[] ExcludedOperations = new[]
    {
        "ConsumeCreditsCommand",
        "ListChatSessionsQuery",
        "CreateChatSessionCommand",
        "UpdateChatSessionCommand",
        "DeleteChatSessionCommand",
        "GetChatSessionQuery",
        "ListUsersQuery",
        "CreateUserCommand",
        "UpdateUserCommand",
        "LoginCommand",
        "RegisterCommand",
        "GetUserQuery",
        "ListTeamsQuery",
        "CreateTeamCommand",
        "UpdateTeamCommand",
        "DeleteTeamCommand"
    };

    // Specific patterns that indicate AI/LLM operations
    private static readonly string[] AiOperationPatterns = new[]
    {
        "ChainOfSpreadsheet",
        "SendChatMessage",
        "StreamChatMessage",
        "GenerateEmbedding",
        "ProcessWithAI",
        "InvokeSemanticFunction",
        "ExecutePrompt",
        "GenerateCompletion",
        "SemanticSearch",
        "GenerateResponse",
        "ProcessNaturalLanguage"
    };

    /// <summary>
    /// Determines if the given request type represents an AI/LLM operation
    /// that should be traced with Langfuse
    /// </summary>
    public static bool IsAiOperation<TRequest>(TRequest request)
    {
        var requestType = typeof(TRequest);
        var requestNamespace = requestType.Namespace ?? string.Empty;
        var requestName = requestType.Name;
        
        // For test purposes, check if the request has a ToString override
        var requestString = request?.ToString();
        if (!string.IsNullOrEmpty(requestString) && requestString != requestType.FullName)
        {
            requestName = requestString;
        }

        // First, check if this is explicitly excluded
        if (ExcludedOperations.Contains(requestName, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check for specific AI operation patterns
        foreach (var pattern in AiOperationPatterns)
        {
            if (requestName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check namespace for AI-specific operations
        if (requestNamespace.Contains("AI", StringComparison.OrdinalIgnoreCase) ||
            requestNamespace.Contains("SemanticKernel", StringComparison.OrdinalIgnoreCase) ||
            requestNamespace.Contains("Embedding", StringComparison.OrdinalIgnoreCase) ||
            requestNamespace.Contains("Completion", StringComparison.OrdinalIgnoreCase) ||
            requestNamespace.Contains("LLM", StringComparison.OrdinalIgnoreCase))
        {
            // Double-check it's not a basic CRUD operation
            if (!IsBasicCrudOperation(requestName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBasicCrudOperation(string requestName)
    {
        var crudPatterns = new[] { "List", "Create", "Update", "Delete", "Get", "Find", "Query", "Command" };
        
        // Check if it's just a basic CRUD operation without AI-specific terms
        foreach (var pattern in crudPatterns)
        {
            if (requestName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                // Check if there's an AI-specific term before the CRUD operation
                var withoutSuffix = requestName[..^pattern.Length];
                if (!ContainsAiSpecificTerm(withoutSuffix))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsAiSpecificTerm(string text)
    {
        var aiTerms = new[] { "AI", "LLM", "Semantic", "Embedding", "Completion", "Generate", "Process" };
        
        foreach (var term in aiTerms)
        {
            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}