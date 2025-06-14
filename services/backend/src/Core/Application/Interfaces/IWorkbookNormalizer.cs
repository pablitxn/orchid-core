using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Normalizes workbook data including header detection, type inference, and named range creation.
/// </summary>
public interface IWorkbookNormalizer
{
    /// <summary>
    /// Normalizes a workbook by detecting headers, inferring types, and creating canonical named ranges.
    /// </summary>
    Task<NormalizedWorkbook> NormalizeAsync(WorkbookEntity workbook, CancellationToken cancellationToken = default);
}