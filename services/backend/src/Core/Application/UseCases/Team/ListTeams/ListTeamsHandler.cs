using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Team.ListTeams;

public class ListTeamsHandler(ITeamRepository repository) : IRequestHandler<ListTeamsQuery, List<TeamEntity>>
{
    private readonly ITeamRepository _repository = repository;

    public async Task<List<TeamEntity>> Handle(ListTeamsQuery request, CancellationToken cancellationToken)
    {
        return await _repository.ListAsync(cancellationToken);
    }
}