namespace Application.Interfaces;

/// <summary>
///     Defines a service for Excel operations used by AskToExcelPlugin.
/// </summary>
public interface IAskToExcelService
{
    /// <summary>
    ///     Identifies Excel columns matching the provided description.
    /// </summary>
    /// <param name="filePath">The path to the Excel file</param>
    /// <param name="sheetName">The name of the worksheet</param>
    /// <param name="description">The description to match columns against</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing matched columns and sample values</returns>
    Task<string> IdentifyExcelColumnAsync(
        string filePath,
        string sheetName,
        string description,
        CancellationToken cancellationToken = default);

    // Add similar documentation and async signatures for other methods
    Task<string> GetCellAsync(
        string filePath,
        string sheetName,
        string valueColumn,
        string key,
        CancellationToken cancellationToken = default);

    Task<string> GetNumericColumnAsync(
        string filePath,
        string sheetName,
        string columnName,
        CancellationToken cancellationToken = default);

    Task<string> GetNumericColumnWhereAsync(
        string filePath,
        string sheetName,
        string targetColumn,
        string filterColumn,
        string filterValue,
        CancellationToken cancellationToken = default);

    Task<string> GetTableAsJsonAsync(
        string filePath,
        string sheetName,
        CancellationToken cancellationToken = default);

    Task<string> WorkbookSchemaAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<string> GroupBySumAsync(
        string filePath,
        string sheetName,
        string keyColumn,
        string valueColumn,
        CancellationToken cancellationToken = default);

    Task<string> GetRowAsync(
        string filePath,
        string sheetName,
        string keyColumn,
        string keyValue,
        CancellationToken cancellationToken = default);
}