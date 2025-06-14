namespace Application.Interfaces;

/// <summary>
///     Defines a service for processing Excel files and returning summaries.
/// </summary>
public interface IExcelProcessorService
{
    /// <summary>
    ///     Processes the Excel file and returns a text summary of its contents.
    /// </summary>
    /// <param name="filePath">The URL or path of the stored file (may be used for reference).</param>
    /// <param name="fileName">The name of the file to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Textual summary of the Excel contents.</returns>
    Task<string> ProcessAsync(string filePath, string fileName, CancellationToken cancellationToken);
}