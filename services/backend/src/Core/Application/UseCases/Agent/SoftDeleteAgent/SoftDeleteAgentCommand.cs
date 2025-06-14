using MediatR;

namespace Application.UseCases.Agent.SoftDeleteAgent;

public sealed record SoftDeleteAgentCommand(Guid AgentId, Guid UserId) : IRequest<Unit>;