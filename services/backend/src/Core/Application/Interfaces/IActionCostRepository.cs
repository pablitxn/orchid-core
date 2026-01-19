using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Repository for tracking costs associated with actions (e.g., LLM calls).
/// </summary>
public interface IActionCostRepository
{
    /// <summary>
    /// Retrieves the last recorded cost entry for a given action type, or <c>null</c> when no record exists.
    /// </summary>
    Task<ActionCostEntity?> GetByActionAsync(string requestActionType, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new action cost entry.
    /// </summary>
    Task<ActionCostEntity> CreateAsync(ActionCostEntity cost, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing action cost entry.
    /// </summary>
    Task UpdateAsync(ActionCostEntity cost, CancellationToken cancellationToken = default);
}