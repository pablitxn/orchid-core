using MediatR;

namespace Application.UseCases.Agent.PermanentDeleteAgent;

public sealed record PermanentDeleteAgentCommand(Guid AgentId, Guid UserId) : IRequest<Unit>;