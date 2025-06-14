using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Repository for tracking costs associated with actions (e.g., LLM calls).
/// </summary>
public interface IActionCostRepository
{
    /// <summary>
    /// Records the cost of an action.
    /// </summary>
    /// <param name="actionType">Type of action (e.g., "table_detection", "natural_language_query")</param>
    /// <param name="cost">Cost in USD</param>
    /// <param name="metadata">Additional metadata about the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordActionCostAsync(
        string actionType,
        decimal cost,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total cost for a specific action type within a time range.
    /// </summary>
    /// <param name="actionType">Type of action</param>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total cost in USD</returns>
    Task<decimal> GetTotalCostAsync(
        string actionType,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cost breakdown by action type for a time range.
    /// </summary>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of action type to total cost</returns>
    Task<Dictionary<string, decimal>> GetCostBreakdownAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the last recorded cost entry for a given action type, or <c>null</c> when no record exists.
    /// </summary>
    /// <param name="requestActionType">Action type identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Action cost entity or <c>null</c>.</returns>
    Task<ActionCostEntity?> GetByActionAsync(string requestActionType, CancellationToken cancellationToken);
}