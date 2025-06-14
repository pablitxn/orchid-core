namespace Domain.Entities.Spreadsheet;

public class WorksheetEntity
{
    public string Name { get; init; } = string.Empty;
    public int HeaderRowIndex { get; set; }
    public List<HeaderEntity> Headers { get; set; } = [];
    public List<List<CellEntity>> Rows { get; set; } = [];
}