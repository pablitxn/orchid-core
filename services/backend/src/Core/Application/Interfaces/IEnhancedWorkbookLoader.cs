using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Loads Excel workbooks with full metadata and structural information.
/// </summary>
public interface IEnhancedWorkbookLoader
{
    /// <summary>
    /// Loads a workbook with full cell metadata and structural information.
    /// </summary>
    /// <param name="filePath">Path to the Excel file.</param>
    /// <param name="options">Loading options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rich workbook context.</returns>
    Task<WorkbookContext> LoadAsync(
        string filePath, 
        WorkbookLoadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a file can be loaded.
    /// </summary>
    Task<bool> CanLoadAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for loading workbooks.
/// </summary>
public sealed class WorkbookLoadOptions
{
    /// <summary>
    /// Maximum cells to load (for memory protection).
    /// </summary>
    public int MaxCellCount { get; init; } = 1_000_000;

    /// <summary>
    /// Include cell styling information.
    /// </summary>
    public bool IncludeStyles { get; init; } = true;

    /// <summary>
    /// Include formulas.
    /// </summary>
    public bool IncludeFormulas { get; init; } = true;

    /// <summary>
    /// Auto-detect tables and structured regions.
    /// </summary>
    public bool DetectTables { get; init; } = true;

    /// <summary>
    /// Memory optimization level.
    /// </summary>
    public MemoryOptimizationLevel MemoryOptimization { get; init; } = MemoryOptimizationLevel.Balanced;
}

public enum MemoryOptimizationLevel
{
    /// <summary>
    /// No optimization, fastest loading.
    /// </summary>
    None,

    /// <summary>
    /// Balanced optimization.
    /// </summary>
    Balanced,

    /// <summary>
    /// Maximum optimization, may be slower.
    /// </summary>
    Maximum
}