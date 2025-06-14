namespace Domain.Entities.Spreadsheet;

/// <summary>
/// Rich workbook context for LLM comprehension with full metadata.
/// </summary>
public sealed class WorkbookContext
{
    /// <summary>
    /// Original file path or identifier.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Workbook-level metadata.
    /// </summary>
    public WorkbookMetadata Metadata { get; init; } = new();

    /// <summary>
    /// All worksheets with their rich cell data.
    /// </summary>
    public List<WorksheetContext> Worksheets { get; init; } = new();

    /// <summary>
    /// Quick statistics for agent decision-making.
    /// </summary>
    public WorkbookStatistics Statistics { get; init; } = new();
}

/// <summary>
/// Workbook metadata.
/// </summary>
public sealed class WorkbookMetadata
{
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTime? CreatedDate { get; init; }
    public DateTime? ModifiedDate { get; init; }
    public string? Author { get; init; }
    public string? LastModifiedBy { get; init; }
    public Dictionary<string, string> CustomProperties { get; init; } = new();
}

/// <summary>
/// Worksheet context with rich cell data.
/// </summary>
public sealed class WorksheetContext
{
    /// <summary>
    /// Worksheet name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Zero-based index in workbook.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// All cells keyed by address (e.g., "A1" -> cell).
    /// </summary>
    public Dictionary<string, EnhancedCellEntity> Cells { get; init; } = new();

    /// <summary>
    /// Worksheet dimensions.
    /// </summary>
    public WorksheetDimensions Dimensions { get; init; } = new();

    /// <summary>
    /// Named ranges in this worksheet.
    /// </summary>
    public List<NamedRange> NamedRanges { get; init; } = new();

    /// <summary>
    /// Detected tables or structured regions.
    /// </summary>
    public List<DetectedTable> DetectedTables { get; init; } = new();
}

/// <summary>
/// Worksheet dimension info.
/// </summary>
public sealed class WorksheetDimensions
{
    public int FirstRowIndex { get; init; }
    public int LastRowIndex { get; init; }
    public int FirstColumnIndex { get; init; }
    public int LastColumnIndex { get; init; }
    public int TotalCells { get; init; }
    public int NonEmptyCells { get; init; }
}

/// <summary>
/// Named range definition.
/// </summary>
public sealed class NamedRange
{
    public string Name { get; init; } = string.Empty;
    public string Range { get; init; } = string.Empty;
    public string? Comment { get; init; }
}

/// <summary>
/// Auto-detected table region.
/// </summary>
public sealed class DetectedTable
{
    public string TableName { get; init; } = string.Empty;
    public string TopLeftAddress { get; init; } = string.Empty;
    public string BottomRightAddress { get; init; } = string.Empty;
    public int HeaderRowIndex { get; init; }
    public List<string> ColumnNames { get; init; } = [];
}

/// <summary>
/// Workbook statistics for compression decisions.
/// </summary>
public sealed class WorkbookStatistics
{
    public int TotalCells { get; init; }
    public int NonEmptyCells { get; init; }
    public double EmptyCellPercentage { get; init; }
    public Dictionary<CellDataType, int> DataTypeDistribution { get; init; } = new();
    public Dictionary<string, int> NumberFormatDistribution { get; init; } = new();
    public int EstimatedTokenCount { get; init; }
    public long MemoryUsageBytes { get; init; }
}