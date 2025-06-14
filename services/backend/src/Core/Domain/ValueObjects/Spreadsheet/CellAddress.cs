namespace Domain.ValueObjects.Spreadsheet;

public readonly struct CellAddress : IEquatable<CellAddress>
{
    public int Row { get; }
    public int Column { get; }
    public string A1Reference { get; }

    public CellAddress(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        Row = row;
        Column = column;
        A1Reference = ToA1Reference(row, column);
    }

    public CellAddress(string a1Reference)
    {
        if (string.IsNullOrWhiteSpace(a1Reference))
            throw new ArgumentException("A1 reference cannot be empty", nameof(a1Reference));

        // Trim whitespace before processing
        var trimmedReference = a1Reference.Trim();

        if (string.IsNullOrEmpty(trimmedReference))
            throw new ArgumentException("A1 reference cannot be empty after trimming", nameof(a1Reference));

        A1Reference = trimmedReference.ToUpperInvariant();
        (Row, Column) = FromA1Reference(A1Reference);
    }

    private static string ToA1Reference(int row, int column)
    {
        var columnName = string.Empty;
        var col = column;

        while (col >= 0)
        {
            columnName = (char)('A' + col % 26) + columnName;
            col = col / 26 - 1;
        }

        return columnName + (row + 1);
    }

    private static (int row, int column) FromA1Reference(string a1Reference)
    {
        var match = System.Text.RegularExpressions.Regex.Match(a1Reference, @"^([A-Z]+)(\d+)$");
        if (!match.Success)
            throw new ArgumentException($"Invalid A1 reference: {a1Reference}");

        var columnName = match.Groups[1].Value;
        var rowNumber = int.Parse(match.Groups[2].Value) - 1;

        var column = 0;
        for (var i = 0; i < columnName.Length; i++)
        {
            column = column * 26 + (columnName[i] - 'A' + 1);
        }

        column--;

        return (rowNumber, column);
    }

    public bool Equals(CellAddress other) => Row == other.Row && Column == other.Column;
    public override bool Equals(object? obj) => obj is CellAddress other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Row, Column);
    public override string ToString() => A1Reference;
    public static bool operator ==(CellAddress left, CellAddress right) => left.Equals(right);
    public static bool operator !=(CellAddress left, CellAddress right) => !left.Equals(right);
}