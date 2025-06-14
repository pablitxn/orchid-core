using MediatR;

namespace Application.UseCases.Agent.VerifyAgentAccess;

public record VerifyAgentAccessQuery(Guid UserId, Guid AgentId) : IRequest<VerifyAgentAccessResult>;

public record VerifyAgentAccessResult(
    bool HasAccess,
    string? Reason = null,
    List<Guid>? MissingPlugins = null
);