using System.Text;
using Application.Interfaces;
using Aspose.Cells;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
///     Processes spreadsheet files using Aspose.Cells and returns a summary of their contents.
/// </summary>
public class AsposeSpreadsheetService(IFileStorageService fileStorage, ILogger<AsposeSpreadsheetService> logger)
    : ISpreadsheetService
{
    private readonly IFileStorageService _fileStorage =
        fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));

    private readonly ILogger<AsposeSpreadsheetService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    // Initialize Aspose.Cells license here if available

    /// <inheritdoc />
    public async Task<string> ProcessAsync(string filePath, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await _fileStorage.GetFileAsync(fileName, cancellationToken);
            // Load workbook from stream
            var workbook = new Workbook(stream);
            var sb = new StringBuilder();
            sb.AppendLine($"Processed spreadsheet: {fileName}");
            foreach (var sheet in workbook.Worksheets)
            {
                // MaxDataRow is zero-based; +1 for total rows count
                var rowCount = sheet.Cells.MaxDataRow + 1;
                sb.AppendLine($"Sheet '{sheet.Name}' has {rowCount} rows.");
            }

            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing spreadsheet file {FileName}", fileName);
            throw;
        }
    }
}