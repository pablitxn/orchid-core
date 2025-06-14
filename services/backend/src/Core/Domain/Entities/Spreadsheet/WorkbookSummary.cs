namespace Domain.Entities.Spreadsheet;

/// <summary>
/// Compact summary of workbook data for LLM consumption.
/// </summary>
public class WorkbookSummary
{
    public string SheetName { get; init; } = string.Empty;
    public Dictionary<string, string> AliasToOriginal { get; set; } = new();
    public List<ColumnSummary> Columns { get; set; } = [];
    public List<Dictionary<string, string>> SampleRows { get; set; } = [];
    public int TotalRows { get; init; }
    public string CompactJson { get; set; } = string.Empty;
}

/// <summary>
/// Summary of a single column.
/// </summary>
public class ColumnSummary
{
    public string Alias { get; init; } = string.Empty;
    public string Original { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public object? Min { get; init; }
    public object? Max { get; init; }
    public double? Mean { get; init; }
    public int UniqueCount { get; init; }
    public List<string> TopValues { get; init; } = [];
}