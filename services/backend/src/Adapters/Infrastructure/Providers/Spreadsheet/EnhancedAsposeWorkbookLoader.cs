using System.Diagnostics;
using System.Drawing;
using Application.Interfaces;
using Aspose.Cells;
using Aspose.Cells.Tables;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Enhanced workbook loader using Aspose.Cells with full metadata extraction.
/// </summary>
public sealed class EnhancedAsposeWorkbookLoader : IEnhancedWorkbookLoader
{
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<EnhancedAsposeWorkbookLoader> _logger;

    public EnhancedAsposeWorkbookLoader(
        IFileStorageService fileStorage,
        ILogger<EnhancedAsposeWorkbookLoader> logger)
    {
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkbookContext> LoadAsync(
        string filePath,
        WorkbookLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new WorkbookLoadOptions();
        
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Loading workbook from {FilePath} with options {@Options}", filePath, options);

        try
        {
            // Extract filename from path
            var fileName = Path.GetFileName(filePath);
            
            await using var stream = await _fileStorage.GetFileAsync(fileName, cancellationToken);
            var fileSize = stream.Length;

            // Configure Aspose load options for memory optimization
            var loadOptions = new LoadOptions(LoadFormat.Xlsx)
            {
                MemorySetting = options.MemoryOptimization switch
                {
                    MemoryOptimizationLevel.None => MemorySetting.Normal,
                    MemoryOptimizationLevel.Maximum => MemorySetting.MemoryPreference,
                    _ => MemorySetting.MemoryPreference
                }
            };

            using var workbook = new Workbook(stream, loadOptions);
            
            var context = new WorkbookContext
            {
                FilePath = filePath,
                Metadata = ExtractMetadata(workbook, fileName, fileSize),
                Worksheets = new List<WorksheetContext>(),
                Statistics = new WorkbookStatistics()
            };

            var totalCells = 0;
            var nonEmptyCells = 0;
            var dataTypeDistribution = new Dictionary<CellDataType, int>();
            var formatDistribution = new Dictionary<string, int>();

            // Process each worksheet
            for (var sheetIndex = 0; sheetIndex < workbook.Worksheets.Count; sheetIndex++)
            {
                var worksheet = workbook.Worksheets[sheetIndex];
                var worksheetContext = await ProcessWorksheetAsync(
                    worksheet, 
                    sheetIndex, 
                    options, 
                    cancellationToken);
                
                context.Worksheets.Add(worksheetContext);
                
                // Update statistics
                totalCells += worksheetContext.Dimensions.TotalCells;
                nonEmptyCells += worksheetContext.Dimensions.NonEmptyCells;
                
                // Aggregate cell type distribution
                foreach (var cell in worksheetContext.Cells.Values)
                {
                    dataTypeDistribution.TryGetValue(cell.DataType, out var count);
                    dataTypeDistribution[cell.DataType] = count + 1;
                    
                    if (!string.IsNullOrEmpty(cell.NumberFormatString))
                    {
                        formatDistribution.TryGetValue(cell.NumberFormatString, out var formatCount);
                        formatDistribution[cell.NumberFormatString] = formatCount + 1;
                    }
                }

                // Check cell count limit
                if (totalCells > options.MaxCellCount)
                {
                    _logger.LogWarning(
                        "Cell count limit exceeded. Loaded {LoadedCells} of {MaxCells} allowed cells", 
                        totalCells, 
                        options.MaxCellCount);
                    break;
                }
            }

            // Create a new context with all data including statistics
            var finalContext = new WorkbookContext
            {
                FilePath = context.FilePath,
                Metadata = context.Metadata,
                Worksheets = context.Worksheets,
                Statistics = new WorkbookStatistics
                {
                    TotalCells = totalCells,
                    NonEmptyCells = nonEmptyCells,
                    EmptyCellPercentage = totalCells > 0 ? (totalCells - nonEmptyCells) * 100.0 / totalCells : 0,
                    DataTypeDistribution = dataTypeDistribution,
                    NumberFormatDistribution = formatDistribution,
                    EstimatedTokenCount = EstimateTokenCount(context),
                    MemoryUsageBytes = GC.GetTotalMemory(false)
                }
            };

            stopwatch.Stop();
            _logger.LogInformation(
                "Successfully loaded workbook with {SheetCount} sheets, {CellCount} cells in {ElapsedMs}ms",
                context.Worksheets.Count,
                totalCells,
                stopwatch.ElapsedMilliseconds);

            return finalContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workbook from {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> CanLoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            // Check supported extensions
            if (!new[] { ".xlsx", ".xls", ".xlsm", ".xlsb" }.Contains(extension))
                return false;

            // Verify file exists and is accessible
            await using var stream = await _fileStorage.GetFileAsync(fileName, cancellationToken);
            return stream.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private WorkbookMetadata ExtractMetadata(Workbook workbook, string fileName, long fileSize)
    {
        var builtInProps = workbook.BuiltInDocumentProperties;
        var customProps = workbook.CustomDocumentProperties;
        
        var metadata = new WorkbookMetadata
        {
            FileName = fileName,
            FileSizeBytes = fileSize,
            Author = builtInProps.Author,
            LastModifiedBy = builtInProps.LastSavedBy,
            CreatedDate = builtInProps.CreatedTime,
            ModifiedDate = builtInProps.LastSavedTime,
            CustomProperties = new Dictionary<string, string>()
        };

        // Extract custom properties
        for (var i = 0; i < customProps.Count; i++)
        {
            var prop = customProps[i];
            metadata.CustomProperties[prop.Name] = prop.Value?.ToString() ?? string.Empty;
        }

        return metadata;
    }

    private async Task<WorksheetContext> ProcessWorksheetAsync(
        Worksheet worksheet,
        int sheetIndex,
        WorkbookLoadOptions options,
        CancellationToken cancellationToken)
    {
        var cellsDictionary = new Dictionary<string, EnhancedCellEntity>();

        var cells = worksheet.Cells;
        var maxRow = cells.MaxDataRow;
        var maxCol = cells.MaxDataColumn;
        var minRow = cells.MinDataRow;
        var minCol = cells.MinDataColumn;

        // Process cells first to count them
        var totalCells = 0;
        var nonEmptyCells = 0;
        
        if (maxRow >= 0 && maxCol >= 0)
        {
            for (var row = minRow; row <= maxRow; row++)
            {
                for (var col = minCol; col <= maxCol; col++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var cell = cells[row, col];
                    var address = CellsHelper.CellIndexToName(row, col);
                    
                    var enhancedCell = CreateEnhancedCell(cell, row, col, address, options);
                    cellsDictionary[address] = enhancedCell;
                    
                    totalCells++;
                    if (enhancedCell.DataType != CellDataType.Empty)
                    {
                        nonEmptyCells++;
                    }
                }
            }
        }


        // Extract named ranges from workbook level (Aspose stores them at workbook level)
        var workbook = worksheet.Workbook;
        var namedRanges = new List<NamedRange>();
        for (var i = 0; i < workbook.Worksheets.Names.Count; i++)
        {
            var namedRange = workbook.Worksheets.Names[i];
            if (namedRange.RefersTo.Contains(worksheet.Name))
            {
                namedRanges.Add(new NamedRange
                {
                    Name = namedRange.Text,
                    Range = namedRange.RefersTo,
                    Comment = namedRange.Comment
                });
            }
        }

        // Detect tables if requested
        var detectedTables = options.DetectTables 
            ? await DetectTablesAsync(worksheet, cancellationToken)
            : new List<DetectedTable>();

        // Create and return the complete worksheet context
        return new WorksheetContext
        {
            Name = worksheet.Name,
            Index = sheetIndex,
            Cells = cellsDictionary,
            Dimensions = new WorksheetDimensions
            {
                FirstRowIndex = minRow >= 0 ? minRow : 0,
                LastRowIndex = maxRow >= 0 ? maxRow : 0,
                FirstColumnIndex = minCol >= 0 ? minCol : 0,
                LastColumnIndex = maxCol >= 0 ? maxCol : 0,
                TotalCells = totalCells,
                NonEmptyCells = nonEmptyCells
            },
            NamedRanges = namedRanges,
            DetectedTables = detectedTables
        };
    }

    private EnhancedCellEntity CreateEnhancedCell(
        Cell cell,
        int row,
        int col,
        string address,
        WorkbookLoadOptions options)
    {
        var cellStyle = cell.GetStyle();
        CellStyleMetadata? styleMetadata = null;

        // Extract style if requested
        if (options.IncludeStyles)
        {
            styleMetadata = new CellStyleMetadata
            {
                IsBold = cellStyle.Font.IsBold,
                BackgroundColor = cellStyle.BackgroundColor.IsEmpty ? null : ColorToHex(cellStyle.BackgroundColor),
                ForegroundColor = cellStyle.ForegroundColor.IsEmpty ? null : ColorToHex(cellStyle.ForegroundColor),
                HasBorders = HasBorders(cellStyle),
                IsMerged = cell.IsMerged,
                MergedRowSpan = cell.GetMergedRange()?.RowCount,
                MergedColumnSpan = cell.GetMergedRange()?.ColumnCount
            };
        }

        var enhancedCell = new EnhancedCellEntity
        {
            Address = address,
            RowIndex = row,
            ColumnIndex = col,
            Value = cell.Value,
            NumberFormatString = cellStyle.Custom ?? string.Empty,
            FormattedValue = cell.StringValue ?? string.Empty,
            DataType = MapCellType(cell.Type),
            Formula = options.IncludeFormulas && cell.IsFormula ? cell.Formula : null,
            Style = styleMetadata
        };

        return enhancedCell;
    }

    private static CellDataType MapCellType(CellValueType asposeType)
    {
        return asposeType switch
        {
            CellValueType.IsNull => CellDataType.Empty,
            CellValueType.IsString => CellDataType.String,
            CellValueType.IsNumeric => CellDataType.Number,
            CellValueType.IsDateTime => CellDataType.DateTime,
            CellValueType.IsBool => CellDataType.Boolean,
            CellValueType.IsError => CellDataType.Error,
            _ => CellDataType.String
        };
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool HasBorders(Style style)
    {
        return style.Borders[BorderType.TopBorder].LineStyle != CellBorderType.None ||
               style.Borders[BorderType.BottomBorder].LineStyle != CellBorderType.None ||
               style.Borders[BorderType.LeftBorder].LineStyle != CellBorderType.None ||
               style.Borders[BorderType.RightBorder].LineStyle != CellBorderType.None;
    }

    private async Task<List<DetectedTable>> DetectTablesAsync(
        Worksheet worksheet,
        CancellationToken cancellationToken)
    {
        var tables = new List<DetectedTable>();

        // Use Aspose's table detection
        foreach (ListObject listObject in worksheet.ListObjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var table = new DetectedTable
            {
                TableName = listObject.DisplayName,
                TopLeftAddress = CellsHelper.CellIndexToName(
                    listObject.StartRow, 
                    listObject.StartColumn),
                BottomRightAddress = CellsHelper.CellIndexToName(
                    listObject.EndRow, 
                    listObject.EndColumn),
                HeaderRowIndex = listObject.StartRow,
                ColumnNames = new List<string>()
            };

            // Extract column names
            for (var col = listObject.StartColumn; col <= listObject.EndColumn; col++)
            {
                var headerCell = worksheet.Cells[listObject.StartRow, col];
                table.ColumnNames.Add(headerCell.StringValue ?? $"Column{col + 1}");
            }

            tables.Add(table);
        }

        return tables;
    }

    private static int EstimateTokenCount(WorkbookContext context)
    {
        // Rough estimation: ~4 characters per token
        var totalChars = 0;
        
        foreach (var worksheet in context.Worksheets)
        {
            foreach (var cell in worksheet.Cells.Values)
            {
                totalChars += cell.FormattedValue?.Length ?? 0;
                totalChars += cell.Address.Length;
                totalChars += cell.NumberFormatString?.Length ?? 0;
            }
        }

        return totalChars / 4;
    }
}