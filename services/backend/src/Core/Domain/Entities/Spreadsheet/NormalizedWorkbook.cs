namespace Domain.Entities.Spreadsheet;

/// <summary>
/// Represents a normalized workbook with type inference and canonical naming.
/// </summary>
public class NormalizedWorkbook
{
    public WorkbookEntity OriginalWorkbook { get; set; } = new();
    public WorksheetEntity MainWorksheet { get; set; } = new();
    public Dictionary<string, ColumnMetadata> ColumnMetadata { get; set; } = new();
    public Dictionary<string, string> AliasToOriginal { get; set; } = new();
    public Dictionary<string, string> OriginalToAlias { get; set; } = new();
}

/// <summary>
/// Metadata for a normalized column.
/// </summary>
public class ColumnMetadata
{
    public string OriginalName { get; init; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public ColumnDataType DataType { get; set; }
    public int ColumnIndex { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public double? Mean { get; set; }
    public int UniqueCount { get; set; }
    public List<string> TopValues { get; set; } = new();
}

/// <summary>
/// Supported column data types.
/// </summary>
public enum ColumnDataType
{
    String,
    Number,
    DateTime,
    Boolean,
}