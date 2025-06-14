using Domain.ValueObjects.Spreadsheet;

namespace Application.Interfaces.Spreadsheet;

/// <summary>
/// Service for detecting table ranges in spreadsheets using LLM analysis.
/// </summary>
public interface ITableDetectionService
{
    /// <summary>
    /// Detects table ranges in compressed spreadsheet text using LLM.
    /// </summary>
    /// <param name="compressedText">The compressed spreadsheet representation</param>
    /// <param name="prompt">Optional additional prompt for table detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected table ranges with confidence scores</returns>
    Task<TableDetectionResult> DetectTablesAsync(
        string compressedText,
        string? prompt = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of table detection operation.
/// </summary>
public record TableDetectionResult(
    List<DetectedTable> Tables,
    int TokensUsed,
    decimal EstimatedCost,
    string? RawLlmResponse);

/// <summary>
/// Represents a detected table in the spreadsheet.
/// </summary>
public record DetectedTable(
    string SheetName,
    int TopRow,
    int LeftColumn,
    int BottomRow,
    int RightColumn,
    double ConfidenceScore,
    string? TableType,
    string? Description)
{
    /// <summary>
    /// Gets the A1-style range notation. Adjusts from 1-based table coordinates to 0-based CellAddress.
    /// </summary>
    public string GetA1Range()
    {
        var topLeft = new CellAddress(TopRow - 1, LeftColumn - 1).A1Reference;
        var bottomRight = new CellAddress(BottomRow - 1, RightColumn - 1).A1Reference;
        return $"{SheetName}!{topLeft}:{bottomRight}";
    }
}