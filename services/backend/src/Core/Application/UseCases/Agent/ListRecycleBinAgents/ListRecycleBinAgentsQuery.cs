using Domain.Entities;
using MediatR;

namespace Application.UseCases.Agent.ListRecycleBinAgents;

public sealed record ListRecycleBinAgentsQuery : IRequest<List<AgentEntity>>;