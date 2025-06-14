using Domain.Entities;

namespace Application.Interfaces;

public interface IAgentRepository
{
    Task<AgentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<List<AgentEntity>> ListAsync(CancellationToken cancellationToken);

    Task CreateAsync(AgentEntity agent, CancellationToken cancellationToken);

    Task UpdateAsync(AgentEntity agent, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<List<AgentEntity>> ListRecycleBinAsync(CancellationToken cancellationToken);

    Task<List<AgentEntity>> ListExpiredRecycleBinAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets an agent with its associated plugins
    /// </summary>
    Task<AgentEntity?> GetWithPluginsAsync(Guid id, CancellationToken cancellationToken);
}