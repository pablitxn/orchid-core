using Domain.Entities;

namespace Application.Interfaces;

public interface ITeamRepository
{
    Task<TeamEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<TeamEntity>> ListAsync(CancellationToken cancellationToken);
    Task CreateAsync(TeamEntity team, CancellationToken cancellationToken);
    Task UpdateAsync(TeamEntity team, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}