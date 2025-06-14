using Domain.Entities;
using MediatR;

namespace Application.UseCases.Team.ListTeams;

public record ListTeamsQuery : IRequest<List<TeamEntity>>;