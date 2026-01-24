using Application.Interfaces;
using Application.UseCases.Team.Common;
using MediatR;

namespace Application.UseCases.Team.ListTeams;

public class ListTeamsHandler(ITeamRepository repository) : IRequestHandler<ListTeamsQuery, List<TeamDto>>
{
    private readonly ITeamRepository _repository = repository;

    public async Task<List<TeamDto>> Handle(ListTeamsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListAsync(cancellationToken);
        return entities.Select(TeamDto.FromEntity).ToList();
    }
}
