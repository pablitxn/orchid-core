using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Aspose.Cells;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.SemanticKernel.Services;

/// <summary>
/// Service for managing sandbox worksheets for safe spreadsheet operations
/// </summary>
public sealed class SandboxManagementService : ISandboxManagementService
{
    private readonly ILogger<SandboxManagementService> _logger;
    private readonly IActivityPublisher _activityPublisher;
    private readonly ISpreadsheetAnalysisService _analysisService;
    
    private const int MaxSandboxRows = 50000;
    private const int MaxSandboxColumns = 100;

    public SandboxManagementService(
        ILogger<SandboxManagementService> logger,
        IActivityPublisher activityPublisher,
        ISpreadsheetAnalysisService analysisService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activityPublisher = activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));
        _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
    }

    /// <inheritdoc/>
    public async Task<SandboxCreationResult> CreateSandboxAsync(
        Workbook workbook,
        Worksheet sourceSheet,
        QueryAnalysisResult analysisResult,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = new SandboxContext
            {
                SandboxName = $"_sandbox_{DateTime.UtcNow:yyyyMMddHHmmss}",
                CreatedAt = DateTime.UtcNow,
                AppliedFilters = analysisResult.Filters
            };

            // Create sandbox worksheet
            var sandbox = workbook.Worksheets.Add(context.SandboxName);
            
            // Get headers and data range
            var headers = _analysisService.ExtractHeaders(sourceSheet);
            var dataRange = _analysisService.GetDataRange(sourceSheet);
            
            // Save original row count
            context.OriginalRowCount = dataRange.LastRow - dataRange.FirstRow + 1;

            // Copy headers
            await CopyHeadersAsync(sourceSheet, sandbox, headers);

            // Determine if we need full dataset
            bool needsFullDataset = analysisResult.RequiresFullDataset || 
                                    QueryNeedsFullDataset(analysisResult);
            
            context.FullDatasetPreserved = needsFullDataset;

            // Copy data based on requirements
            if (needsFullDataset)
            {
                context.FilteredRowCount = await CopyAllDataAsync(
                    sourceSheet, sandbox, headers, dataRange);
            }
            else
            {
                context.FilteredRowCount = await CopyFilteredDataAsync(
                    sourceSheet, sandbox, headers, dataRange, analysisResult.Filters);
            }

            // Calculate column statistics
            await CalculateColumnStatsAsync(sandbox, headers, context);

            // Add metadata columns if needed
            await AddMetadataColumnsAsync(sandbox, headers.Count, analysisResult);

            await _activityPublisher.PublishAsync("sandbox.created", new
            {
                sandboxName = context.SandboxName,
                originalRows = context.OriginalRowCount,
                filteredRows = context.FilteredRowCount,
                fullDatasetPreserved = context.FullDatasetPreserved,
                columnCount = headers.Count
            });

            return new SandboxCreationResult
            {
                Sandbox = sandbox,
                Context = context,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create sandbox");
            
            return new SandboxCreationResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task ApplyHelperColumnsAsync(
        Worksheet sandbox,
        FormulaStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        if (!strategy.HelperColumns.Any()) return;

        await Task.CompletedTask;

        var headers = _analysisService.ExtractHeaders(sandbox);
        var dataRows = _analysisService.CountDataRows(sandbox);
        int nextCol = headers.Count;

        foreach (var helper in strategy.HelperColumns)
        {
            // Add header
            sandbox.Cells[0, nextCol].Value = helper.Name;
            helper.ColumnIndex = nextCol;

            // Apply formula to all data rows
            for (int row = 1; row <= dataRows; row++)
            {
                // Adjust formula for current row
                var adjustedFormula = AdjustFormulaForRow(helper.Formula, row + 1);
                sandbox.Cells[row, nextCol].Formula = adjustedFormula;
            }

            await _activityPublisher.PublishAsync("sandbox.helper_column_added", new
            {
                columnName = helper.Name,
                columnIndex = nextCol,
                formula = helper.Formula,
                purpose = helper.Purpose
            });

            nextCol++;
        }

        // Recalculate formulas
        sandbox.CalculateFormula(""); // todo: fix me
    }

    /// <inheritdoc/>
    public void CleanupSandbox(Workbook workbook, Worksheet sandbox)
    {
        try
        {
            var sandboxName = sandbox.Name;
            var index = workbook.Worksheets.IndexOf(sandbox);
            
            if (index >= 0)
            {
                workbook.Worksheets.RemoveAt(index);
                
                _logger.LogInformation("Cleaned up sandbox: {SandboxName}", sandboxName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup sandbox sheet");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateSandboxAsync(
        Worksheet sandbox,
        SandboxContext context,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        try
        {
            // Validate row count
            var actualRows = _analysisService.CountDataRows(sandbox);
            if (actualRows != context.FilteredRowCount)
            {
                _logger.LogWarning(
                    "Sandbox row count mismatch. Expected: {Expected}, Actual: {Actual}",
                    context.FilteredRowCount, actualRows);
                return false;
            }

            // Validate headers
            var headers = _analysisService.ExtractHeaders(sandbox);
            if (!headers.Any())
            {
                _logger.LogWarning("Sandbox has no headers");
                return false;
            }

            // Validate data integrity
            var sampleSize = Math.Min(10, actualRows);
            for (int row = 1; row <= sampleSize; row++)
            {
                bool hasData = false;
                for (int col = 0; col < headers.Count; col++)
                {
                    if (sandbox.Cells[row, col].Value != null)
                    {
                        hasData = true;
                        break;
                    }
                }

                if (!hasData)
                {
                    _logger.LogWarning("Sandbox has empty data rows");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate sandbox");
            return false;
        }
    }

    #region Private Methods

    private async Task CopyHeadersAsync(Worksheet source, Worksheet target, List<HeaderInfo> headers)
    {
        await Task.CompletedTask;

        for (int col = 0; col < headers.Count && col < MaxSandboxColumns; col++)
        {
            target.Cells[0, col].Value = headers[col].Name;
            
            // Copy header formatting from the actual header row
            target.Cells[0, col].SetStyle(source.Cells[headers[col].RowIndex, col].GetStyle());
        }
    }

    private async Task<int> CopyAllDataAsync(
        Worksheet source, 
        Worksheet target, 
        List<HeaderInfo> headers, 
        (int FirstRow, int LastRow) dataRange)
    {
        await Task.CompletedTask;
        
        int targetRow = 1;
        int copiedRows = 0;

        for (int row = dataRange.FirstRow; row <= dataRange.LastRow && targetRow <= MaxSandboxRows; row++)
        {
            // Copy row data
            for (int col = 0; col < headers.Count && col < MaxSandboxColumns; col++)
            {
                var cell = source.Cells[row, col];
                var targetCell = target.Cells[targetRow, col];
                
                // Copy value preserving type
                CopyCellValue(cell, targetCell);
            }
            
            targetRow++;
            copiedRows++;
        }

        _logger.LogInformation(
            "Copied all data: {CopiedRows} of {TotalRows} rows",
            copiedRows, dataRange.LastRow - dataRange.FirstRow + 1);

        return copiedRows;
    }

    private async Task<int> CopyFilteredDataAsync(
        Worksheet source, 
        Worksheet target, 
        List<HeaderInfo> headers, 
        (int FirstRow, int LastRow) dataRange,
        List<FilterCriteria> filters)
    {
        await Task.CompletedTask;
        
        int targetRow = 1;
        int copiedRows = 0;
        int filteredOutCount = 0;

        for (int row = dataRange.FirstRow; row <= dataRange.LastRow && targetRow <= MaxSandboxRows; row++)
        {
            if (_analysisService.RowMatchesFilters(source, row, headers, filters))
            {
                // Copy row data
                for (int col = 0; col < headers.Count && col < MaxSandboxColumns; col++)
                {
                    var cell = source.Cells[row, col];
                    var targetCell = target.Cells[targetRow, col];
                    
                    CopyCellValue(cell, targetCell);
                }
                
                targetRow++;
                copiedRows++;
            }
            else
            {
                filteredOutCount++;
            }
        }

        _logger.LogInformation(
            "Copied filtered data: {CopiedRows} rows, filtered out {FilteredOut} rows",
            copiedRows, filteredOutCount);

        return copiedRows;
    }

    private void CopyCellValue(Cell source, Cell target)
    {
        // Copy value preserving type
        if (source.Value == null)
        {
            target.Value = null;
            return;
        }

        // Copy based on cell type
        switch (source.Type)
        {
            case CellValueType.IsNumeric:
                target.PutValue(source.DoubleValue);
                break;
                
            case CellValueType.IsDateTime:
                target.PutValue(source.DateTimeValue);
                break;
                
            case CellValueType.IsBool:
                target.PutValue(source.BoolValue);
                break;
                
            case CellValueType.IsString:
            default:
                target.PutValue(source.StringValue);
                break;
        }

        // Copy formula if present
        if (!string.IsNullOrEmpty(source.Formula))
        {
            target.Formula = source.Formula;
        }

        // Copy number format
        var style = source.GetStyle();
        if (!string.IsNullOrEmpty(style.Number.ToString()))
        {
            var targetStyle = target.GetStyle();
            targetStyle.Number = style.Number;
            target.SetStyle(targetStyle);
        }
    }

    private async Task CalculateColumnStatsAsync(
        Worksheet sandbox, 
        List<HeaderInfo> headers, 
        SandboxContext context)
    {
        await Task.CompletedTask;
        
        var dataRows = _analysisService.CountDataRows(sandbox);
        
        for (int col = 0; col < headers.Count; col++)
        {
            int numericCount = 0;
            int nonNullCount = 0;
            
            for (int row = 1; row <= dataRows; row++)
            {
                var cell = sandbox.Cells[row, col];
                if (cell.Value != null)
                {
                    nonNullCount++;
                    
                    if (cell.Type == CellValueType.IsNumeric || 
                        TryParseNumeric(cell.Value, out _))
                    {
                        numericCount++;
                    }
                }
            }
            
            if (nonNullCount > 0)
            {
                context.ColumnStats[headers[col].Name] = numericCount;
            }
        }
    }

    private async Task AddMetadataColumnsAsync(
        Worksheet sandbox, 
        int startCol, 
        QueryAnalysisResult analysis)
    {
        await Task.CompletedTask;

        // Add row number column if needed for complex calculations
        if (analysis.RequiresCalculation && 
            analysis.CalculationSteps.Any(s => 
                s.Contains("row", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("index", StringComparison.OrdinalIgnoreCase)))
        {
            var rowNumCol = startCol;
            sandbox.Cells[0, rowNumCol].Value = "_RowNum";
            
            var dataRows = _analysisService.CountDataRows(sandbox);
            for (int row = 1; row <= dataRows; row++)
            {
                sandbox.Cells[row, rowNumCol].PutValue(row);
            }
        }
    }

    private bool QueryNeedsFullDataset(QueryAnalysisResult analysis)
    {
        // Additional checks beyond what's in the analysis
        return analysis.CalculationSteps.Any(step => 
            step.Contains("ratio", StringComparison.OrdinalIgnoreCase) ||
            step.Contains("comparison", StringComparison.OrdinalIgnoreCase) ||
            step.Contains("versus", StringComparison.OrdinalIgnoreCase));
    }

    private string AdjustFormulaForRow(string formula, int rowNumber)
    {
        // Simple row number replacement
        // This is a basic implementation - could be enhanced with proper formula parsing
        return formula
            .Replace("$2", $"${rowNumber}")
            .Replace("2:", $"{rowNumber}:")
            .Replace(":2", $":{rowNumber}");
    }

    private bool TryParseNumeric(object value, out double result)
    {
        result = 0;
        
        if (value == null) return false;
        
        return value switch
        {
            double d => (result = d, true).Item2,
            int i => (result = i, true).Item2,
            decimal dec => (result = (double)dec, true).Item2,
            string s => double.TryParse(s.Trim().Replace("$", "").Replace(",", ""), out result),
            _ => double.TryParse(value.ToString(), out result)
        };
    }

    #endregion
}