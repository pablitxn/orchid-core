using Application.UseCases.Agent.Common;
using MediatR;

namespace Application.UseCases.Agent.ListRecycleBinAgents;

public sealed record ListRecycleBinAgentsQuery(Guid UserId) : IRequest<List<AgentDto>>;