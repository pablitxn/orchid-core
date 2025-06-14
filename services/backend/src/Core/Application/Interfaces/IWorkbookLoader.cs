using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Loads Excel workbooks and extracts semantic metadata.
/// </summary>
public interface IWorkbookLoader
{
    Task<WorkbookEntity> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
