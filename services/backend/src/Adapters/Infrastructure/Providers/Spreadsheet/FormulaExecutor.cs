using System.Diagnostics;
using Application.Interfaces;
using Aspose.Cells;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Executes Excel formulas in a safe, isolated environment.
/// </summary>
public sealed class FormulaExecutor(
    IFileStorageService fileStorage,
    ILogger<FormulaExecutor> logger) : IFormulaExecutor
{
    private readonly IFileStorageService _fileStorage = fileStorage;
    private readonly ILogger<FormulaExecutor> _logger = logger;

    public async Task<FormulaResult> ExecuteAsync(
        string formula,
        string filePath,
        string worksheetName,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing formula: {Formula} on sheet: {Sheet}", formula, worksheetName);

        var stopwatch = Stopwatch.StartNew();
        var result = new FormulaResult();

        try
        {
            // Load workbook
            await using var stream = await _fileStorage.GetFileAsync(Path.GetFileName(filePath), cancellationToken);
            var workbook = new Workbook(stream);

            // Find worksheet
            var worksheet = string.IsNullOrWhiteSpace(worksheetName)
                ? workbook.Worksheets[0]
                : workbook.Worksheets.Cast<Worksheet>()
                      .FirstOrDefault(ws => ws.Name.Equals(worksheetName, StringComparison.OrdinalIgnoreCase))
                  ?? throw new InvalidOperationException($"Worksheet '{worksheetName}' not found");

            // Create temporary sheet for formula execution
            var tempSheetName = GenerateTempSheetName();
            var tempSheet = workbook.Worksheets.Add(tempSheetName);

            try
            {
                // Prepare formula with sheet references
                var preparedFormula = PrepareFormula(formula, worksheet.Name);

                // Set formula in temporary cell
                var evalCell = tempSheet.Cells["A1"];
                evalCell.Formula = preparedFormula;

                // Calculate with timeout
                var calculateTask = Task.Run(() =>
                {
                    var options = new CalculationOptions
                    {
                        IgnoreError = false,
                        Recursive = true,
                        // PrecisionAsDisplayed = false // todo: fix me
                    };
                    workbook.CalculateFormula(options);
                }, cancellationToken);

                var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
                if (!calculateTask.Wait(effectiveTimeout))
                {
                    throw new TimeoutException(
                        $"Formula execution timed out after {effectiveTimeout.TotalSeconds} seconds");
                }

                // Extract result
                var cellValue = evalCell.Value;
                var cellType = evalCell.Type;

                // Check for errors
                if (evalCell.IsErrorValue)
                {
                    result.Success = false;
                    result.Error = evalCell.StringValue;
                    result.ResultType = FormulaResultType.Error;
                }
                else
                {
                    result.Success = true;
                    result.Value = ConvertCellValue(cellValue, cellType);
                    result.ResultType = DetermineResultType(evalCell);

                    // Handle array formulas
                    if (evalCell.IsArrayHeader)
                    {
                        result.MatrixValue = ExtractArrayResult(tempSheet, evalCell);
                    }
                }
            }
            finally
            {
                // Clean up temporary sheet
                workbook.Worksheets.RemoveAt(workbook.Worksheets.IndexOf(tempSheet));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing formula");
            result.Success = false;
            result.Error = ex.Message;
            result.ResultType = FormulaResultType.Error;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        _logger.LogInformation("Formula execution completed in {Ms}ms. Success: {Success}",
            result.ExecutionTime.TotalMilliseconds, result.Success);

        return result;
    }

    private static string GenerateTempSheetName()
    {
        var guid = Guid.NewGuid().ToString("N");
        var name = $"Temp_{guid}";

        // Excel sheet names have a 31-character limit
        if (name.Length > 31)
        {
            name = name.Substring(0, 31);
        }

        return name;
    }

    private static string PrepareFormula(string formula, string worksheetName)
    {
        // Ensure formula starts with =
        if (!formula.TrimStart().StartsWith("="))
        {
            formula = "=" + formula;
        }

        // If formula doesn't contain sheet references, add default sheet prefix to ranges
        if (!formula.Contains("!"))
        {
            // This is a simplified approach - in production, use proper formula parsing
            // Add sheet reference to column names and ranges
            var pattern = @"\b([A-Z]+\d*(?::[A-Z]+\d*)?)\b";
            formula = System.Text.RegularExpressions.Regex.Replace(formula, pattern, $"{worksheetName}!$1");
        }

        return formula;
    }

    private static object? ConvertCellValue(object? value, CellValueType type)
    {
        return type switch
        {
            CellValueType.IsNumeric => value,
            CellValueType.IsDateTime => value,
            CellValueType.IsBool => value,
            CellValueType.IsString => value?.ToString(),
            CellValueType.IsNull => null,
            _ => value?.ToString()
        };
    }

    private static FormulaResultType DetermineResultType(Cell cell)
    {
        if (cell.IsErrorValue)
            return FormulaResultType.Error;

        if (cell.IsArrayHeader)
            return FormulaResultType.Matrix;

        return FormulaResultType.SingleValue;
    }

    private static List<List<object>>? ExtractArrayResult(Worksheet sheet, Cell arrayHeader)
    {
        var result = new List<List<object>>();

        // Get array dimensions
        var range = arrayHeader.GetArrayRange();
        // if (range == null) return null;

        for (int row = range.StartRow; row <= range.StartRow + range.EndRow - 1; row++)
        {
            var rowData = new List<object>();
            for (int col = range.StartColumn; col <= range.StartColumn + range.EndColumn - 1; col++)
            {
                var cell = sheet.Cells[row, col];
                rowData.Add(ConvertCellValue(cell.Value, cell.Type) ?? string.Empty);
            }

            result.Add(rowData);
        }

        return result;
    }
}