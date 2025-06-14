namespace Domain.Entities.Spreadsheet;

/// <summary>
/// Rich cell representation with full metadata for LLM comprehension.
/// </summary>
public sealed class EnhancedCellEntity
{
    /// <summary>
    /// Cell address in A1 notation (e.g., "A1", "B2", "AA123").
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// Zero-based row index.
    /// </summary>
    public int RowIndex { get; init; }

    /// <summary>
    /// Zero-based column index.
    /// </summary>
    public int ColumnIndex { get; init; }

    /// <summary>
    /// Raw cell value as stored in Excel.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Excel NumberFormatString (e.g., "#,##0.00", "dd/mm/yyyy").
    /// </summary>
    public string NumberFormatString { get; init; } = string.Empty;

    /// <summary>
    /// Formatted display value applying the NumberFormatString.
    /// </summary>
    public string FormattedValue { get; init; } = string.Empty;

    /// <summary>
    /// Cell data type inferred from content.
    /// </summary>
    public CellDataType DataType { get; init; }

    /// <summary>
    /// Formula if this is a formula cell.
    /// </summary>
    public string? Formula { get; init; }

    /// <summary>
    /// Cell style metadata.
    /// </summary>
    public CellStyleMetadata? Style { get; init; }
}

/// <summary>
/// Cell data types.
/// </summary>
public enum CellDataType
{
    Empty,
    String,
    Number,
    DateTime,
    Boolean,
    Formula,
    Error
}

/// <summary>
/// Cell style information relevant for structural analysis.
/// </summary>
public sealed class CellStyleMetadata
{
    public bool IsBold { get; init; }
    public string? BackgroundColor { get; init; }
    public string? ForegroundColor { get; init; }
    public bool HasBorders { get; init; }
    public bool IsMerged { get; init; }
    public int? MergedRowSpan { get; init; }
    public int? MergedColumnSpan { get; init; }
}