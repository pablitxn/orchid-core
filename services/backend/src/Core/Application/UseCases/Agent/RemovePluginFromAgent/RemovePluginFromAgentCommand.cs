using MediatR;

namespace Application.UseCases.Agent.RemovePluginFromAgent;

public sealed record RemovePluginFromAgentCommand(
    Guid UserId,
    Guid AgentId,
    Guid PluginId
) : IRequest<RemovePluginFromAgentDto>;

public sealed record RemovePluginFromAgentDto(
    bool Success,
    string? ErrorMessage = null
);