namespace Application.Interfaces;

/// <summary>
///     Defines a generic service for interacting with spreadsheet files (e.g., .xlsx, .xlsm).
/// </summary>
public interface ISpreadsheetService
{
    /// <summary>
    ///     Processes the spreadsheet and returns a text summary of its contents.
    /// </summary>
    /// <param name="filePath">The URL or path of the stored file (may be used for reference).</param>
    /// <param name="fileName">The name of the file to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Textual summary of the spreadsheet contents.</returns>
    Task<string> ProcessAsync(string filePath, string fileName, CancellationToken cancellationToken);
}