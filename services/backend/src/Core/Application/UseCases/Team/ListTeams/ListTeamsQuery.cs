using Application.UseCases.Team.Common;
using MediatR;

namespace Application.UseCases.Team.ListTeams;

public record ListTeamsQuery : IRequest<List<TeamDto>>;
