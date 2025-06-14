using MediatR;

namespace Application.UseCases.Agent.AddPluginToAgent;

public sealed record AddPluginToAgentCommand(
    Guid UserId,
    Guid AgentId,
    Guid PluginId
) : IRequest<AddPluginToAgentResult>;

public sealed record AddPluginToAgentResult(
    bool Success,
    string? ErrorMessage = null
);