namespace Domain.ValueObjects.Spreadsheet;

public readonly record struct CellData
{
    public CellAddress Address { get; init; }
    public object? Value { get; init; }
    public string? NumberFormatString { get; init; }
    
    public bool IsEmpty => Value is null || (Value is string str && string.IsNullOrWhiteSpace(str));
    
    public string GetStringValue()
    {
        return Value?.ToString() ?? string.Empty;
    }
    
    public string GetHashInput()
    {
        return $"{Address}|{Value}|{NumberFormatString}";
    }
}