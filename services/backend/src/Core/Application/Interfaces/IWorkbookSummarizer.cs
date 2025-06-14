using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Summarizes workbook data for LLM consumption.
/// </summary>
public interface IWorkbookSummarizer
{
    /// <summary>
    /// Creates a compact summary of the workbook suitable for LLM context.
    /// </summary>
    Task<WorkbookSummary> SummarizeAsync(
        NormalizedWorkbook workbook, 
        int sampleSize = 20,
        CancellationToken cancellationToken = default);
}