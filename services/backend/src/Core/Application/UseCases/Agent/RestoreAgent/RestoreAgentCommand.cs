using MediatR;

namespace Application.UseCases.Agent.RestoreAgent;

public sealed record RestoreAgentCommand(Guid AgentId) : IRequest<Unit>;