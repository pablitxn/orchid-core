namespace Domain.Entities.Spreadsheet;

public class CellEntity
{
    public int RowIndex { get; set; }
    public int ColumnIndex { get; init; }
    public string Value { get; init; } = string.Empty;
}