using MediatR;

namespace Application.UseCases.Agent.PermanentDeleteAgent;

public sealed record PermanentDeleteAgentCommand(Guid AgentId) : IRequest<Unit>;