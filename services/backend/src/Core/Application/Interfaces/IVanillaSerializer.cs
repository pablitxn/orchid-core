using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Serializes workbook data to vanilla Markdown-like format for LLM consumption.
/// </summary>
public interface IVanillaSerializer
{
    /// <summary>
    /// Serializes a workbook context to a compact string format.
    /// </summary>
    /// <param name="context">Workbook context to serialize.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>Serialized string representation.</returns>
    ReadOnlyMemory<char> Serialize(WorkbookContext context, VanillaSerializationOptions? options = null);

    /// <summary>
    /// Serializes a specific worksheet.
    /// </summary>
    ReadOnlyMemory<char> SerializeWorksheet(WorksheetContext worksheet, VanillaSerializationOptions? options = null);

    /// <summary>
    /// Estimates token count for serialized output.
    /// </summary>
    int EstimateTokenCount(WorkbookContext context, VanillaSerializationOptions? options = null);
}

/// <summary>
/// Options for vanilla serialization.
/// </summary>
public sealed class VanillaSerializationOptions
{
    /// <summary>
    /// Include empty cells in output.
    /// </summary>
    public bool IncludeEmptyCells { get; init; } = true;

    /// <summary>
    /// Include number format strings.
    /// </summary>
    public bool IncludeNumberFormats { get; init; } = true;

    /// <summary>
    /// Include cell formulas.
    /// </summary>
    public bool IncludeFormulas { get; init; } = false;

    /// <summary>
    /// Include style information.
    /// </summary>
    public bool IncludeStyles { get; init; } = false;

    /// <summary>
    /// Cell separator (default: ", ").
    /// </summary>
    public string CellSeparator { get; init; } = ", ";

    /// <summary>
    /// Row separator (default: "\n").
    /// </summary>
    public string RowSeparator { get; init; } = "\n";

    /// <summary>
    /// Maximum cells to serialize (for safety).
    /// </summary>
    public int? MaxCells { get; init; }
}