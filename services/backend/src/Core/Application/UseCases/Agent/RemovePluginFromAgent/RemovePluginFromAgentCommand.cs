using MediatR;

namespace Application.UseCases.Agent.RemovePluginFromAgent;

public sealed record RemovePluginFromAgentCommand(
    Guid UserId,
    Guid AgentId,
    Guid PluginId
) : IRequest<RemovePluginFromAgentResult>;

public sealed record RemovePluginFromAgentResult(
    bool Success,
    string? ErrorMessage = null
);