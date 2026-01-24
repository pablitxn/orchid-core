using MediatR;

namespace Application.UseCases.Agent.VerifyAgentAccess;

public record VerifyAgentAccessQuery(Guid UserId, Guid AgentId) : IRequest<VerifyAgentAccessDto>;

public record VerifyAgentAccessDto(
    bool HasAccess,
    string? Reason = null,
    List<Guid>? MissingPlugins = null
);