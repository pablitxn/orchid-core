namespace Domain.ValueObjects.Spreadsheet;

public sealed class WorkbookContext
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<WorksheetContext> Worksheets { get; init; } = [];
    public WorkbookStatistics Statistics { get; init; } = new();
}

public sealed class WorksheetContext
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<CellData> Cells { get; init; } = [];
    public (int Rows, int Columns) Dimensions { get; init; }
}

public sealed class WorkbookStatistics
{
    public int TotalCells { get; init; }
    public int EmptyCells { get; init; }
    public double EmptyPercentage => TotalCells > 0 ? (double)EmptyCells / TotalCells * 100 : 0;
    public IReadOnlyDictionary<Type, int> TypeDistribution { get; init; } = new Dictionary<Type, int>();
}