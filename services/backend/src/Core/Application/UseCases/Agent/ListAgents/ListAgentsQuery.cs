using Application.UseCases.Agent.Common;
using MediatR;

namespace Application.UseCases.Agent.ListAgents;

public record ListAgentsQuery(Guid? UserId = null) : IRequest<List<AgentDto>>;