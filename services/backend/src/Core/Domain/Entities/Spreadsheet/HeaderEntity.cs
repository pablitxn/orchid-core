namespace Domain.Entities.Spreadsheet;

public class HeaderEntity
{
    public string Name { get; init; } = string.Empty;
    public List<string> Synonyms { get; init; } = [];
    public int ColumnIndex { get; init; }
    public bool IsKey { get; init; }
}