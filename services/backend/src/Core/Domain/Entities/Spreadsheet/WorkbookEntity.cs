namespace Domain.Entities.Spreadsheet;

public class WorkbookEntity
{
    public List<WorksheetEntity> Worksheets { get; init; } = new();
}
