using MediatR;

namespace Application.UseCases.Agent.AddPluginToAgent;

public sealed record AddPluginToAgentCommand(
    Guid UserId,
    Guid AgentId,
    Guid PluginId
) : IRequest<AddPluginToAgentDto>;

public sealed record AddPluginToAgentDto(
    bool Success,
    string? ErrorMessage = null
);