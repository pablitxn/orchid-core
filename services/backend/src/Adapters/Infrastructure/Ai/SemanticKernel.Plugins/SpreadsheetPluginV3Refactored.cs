using System.ComponentModel;
using System.Text.Json;
using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Aspose.Cells;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Infrastructure.Ai.SemanticKernel.Plugins;

/// <summary>
/// SpreadsheetPluginV3 (Refactored): Enhanced Excel plugin using decoupled services for better maintainability
/// </summary>
public sealed class SpreadsheetPluginV3Refactored(
    ILogger<SpreadsheetPluginV3Refactored> logger,
    IMemoryCache memoryCache,
    IDistributedCache distributedCache,
    IFileStorageService fileStorage,
    IActivityPublisher activityPublisher,
    ISpreadsheetAnalysisService analysisService,
    ISandboxManagementService sandboxService,
    IFormulaExecutionService formulaService)
{
    private readonly ILogger<SpreadsheetPluginV3Refactored> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IMemoryCache _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

    private readonly IDistributedCache _distributedCache =
        distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));

    private readonly IFileStorageService _fileStorage =
        fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));

    private readonly IActivityPublisher _activityPublisher =
        activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));

    private readonly ISpreadsheetAnalysisService _analysisService =
        analysisService ?? throw new ArgumentNullException(nameof(analysisService));

    private readonly ISandboxManagementService _sandboxService =
        sandboxService ?? throw new ArgumentNullException(nameof(sandboxService));

    private readonly IFormulaExecutionService _formulaService =
        formulaService ?? throw new ArgumentNullException(nameof(formulaService));

    [KernelFunction("query_spreadsheet")]
    [Description("Queries Excel data using natural language with high accuracy through sandbox execution")]
    public async Task<string> QuerySpreadsheetAsync(
        [Description("Full path of the workbook")]
        string filePath,
        [Description("Natural language query")]
        string query,
        [Description("Sheet name (optional)")] string sheetName = "")
    {
        _logger.LogInformation("QuerySpreadsheet V3 Refactored: {File} - '{Query}'", filePath, query);

        // Log the start of the query process
        await _activityPublisher.PublishAsync("query_spreadsheet.start", new
        {
            filePath,
            query,
            sheetName,
            timestamp = DateTime.UtcNow,
            version = "V3Refactored"
        });

        try
        {
            // TODO: detect the sheet name if not provided - fallback to first sheet

            // Step 1: Load workbook
            using var workbook = await LoadWorkbookAsync(filePath);
            var sourceSheet = GetWorksheet(workbook, sheetName);

            // Step 2: Detect document format using stratified sampling
            var format = await _analysisService.DetectDocumentFormatAsync(sourceSheet);

            // Step 3: Validate format
            if (format != DocumentFormat.Columnar)
            {
                throw new InvalidOperationException(
                    $"Unsupported spreadsheet format: {format}. Only columnar format is currently supported.");
            }

            // Step 4: Extract headers from the source sheet
            var headers = _analysisService.ExtractHeaders(sourceSheet);
            await _activityPublisher.PublishAsync("query_spreadsheet.ExtractHeaders", new
            {
                filePath,
                sheetName = sourceSheet.Name,
                rowCount = sourceSheet.Cells.MaxRow + 1,
                columnCount = sourceSheet.Cells.MaxColumn + 1,
                headers
            });

            // Step 5: Extract metadata based on detected format
            var metadata = await _analysisService.ExtractDocumentMetadataAsync(sourceSheet, format, headers);

            // Step 6: Analyze the query and the navigate spreadsheet to get the necessary context
            var analysisResult = await _analysisService.AnalyzeQueryAsync(query, metadata, sourceSheet);

            // Extract execution plan from the artifact
            ExecutionPlanDto executionPlan;
            using (var doc = JsonDocument.Parse(analysisResult.Artifact))
            {
                var executionPlanElement = doc.RootElement.GetProperty("ExecutionPlan");
                executionPlan = JsonSerializer.Deserialize<ExecutionPlanDto>(executionPlanElement.GetRawText())
                                ?? new ExecutionPlanDto();
            }

            await _activityPublisher.PublishAsync("query_spreadsheet.AnalyzeQueryAsync", new
            {
                needRunFormula = executionPlan.NeedRunFormula,
                formula = executionPlan.Formula,
                simpleAnswer = executionPlan.SimpleAnswer,
                reasoning = executionPlan.Reasoning,
                artifactsRowCount = executionPlan.ArtifactsFormatted?.Count ?? 0,
                artifactFormatted = executionPlan.ArtifactsFormatted?.Select(row => string.Join(", ", row)).ToList() ??
                                    new List<string>()
            });

            // Check if we need to run a formula or just return the simple answer
            // todo: fix -> send it as a message
            if (!executionPlan.NeedRunFormula)
            {
                var simpleResult = new
                {
                    Success = true,
                    Query = query,
                    Answer = executionPlan.SimpleAnswer,
                    Reasoning = executionPlan.Reasoning,
                    RequiredCalculation = false,
                    DatasetContext = new
                    {
                        HeaderRowIndex = metadata.DataStartRow - 1,
                        DataStartRow = metadata.DataStartRow,
                        DataEndRow = metadata.TotalRows - 1,
                        TotalDataRows = metadata.DataRowCount,
                        Explanation =
                            $"The dataset has headers at row {metadata.DataStartRow - 1} and contains {metadata.DataRowCount} data rows (from row {metadata.DataStartRow} to {metadata.TotalRows - 1})"
                    }
                };

                await _activityPublisher.PublishAsync("query_spreadsheet.completed", new
                {
                    filePath,
                    query,
                    result = simpleResult,
                    success = true,
                    executionTime = DateTime.UtcNow,
                    simpleAnswer = true
                });

                PublishTool("query_spreadsheet", new { filePath, query, sheetName },
                    JsonSerializer.Serialize(simpleResult));

                return JsonSerializer.Serialize(simpleResult);
            }

            // Step 7: Create dynamic spreadsheet from artifacts
            var dynamicWorkbook = new Workbook();
            var dynamicSheet = dynamicWorkbook.Worksheets[0];
            dynamicSheet.Name = "DynamicData";

            // Populate the dynamic sheet with artifacts data
            if (executionPlan.ArtifactsFormatted != null && executionPlan.ArtifactsFormatted.Count > 0)
            {
                for (int row = 0; row < executionPlan.ArtifactsFormatted.Count; row++)
                {
                    var rowData = executionPlan.ArtifactsFormatted[row];
                    for (int col = 0; col < rowData.Count; col++)
                    {
                        var cellValue = rowData[col];
                        if (cellValue != null)
                        {
                            // Preserve data types
                            if (cellValue is JsonElement jsonElement)
                            {
                                switch (jsonElement.ValueKind)
                                {
                                    case JsonValueKind.Number:
                                        dynamicSheet.Cells[row, col].Value = jsonElement.GetDouble();
                                        break;
                                    case JsonValueKind.String:
                                        dynamicSheet.Cells[row, col].Value = jsonElement.GetString();
                                        break;
                                    case JsonValueKind.True:
                                    case JsonValueKind.False:
                                        dynamicSheet.Cells[row, col].Value = jsonElement.GetBoolean();
                                        break;
                                    default:
                                        dynamicSheet.Cells[row, col].Value = jsonElement.ToString();
                                        break;
                                }
                            }
                            else
                            {
                                dynamicSheet.Cells[row, col].Value = cellValue;
                            }
                        }
                    }
                }
            }

            await _activityPublisher.PublishAsync("query_spreadsheet.DynamicSheetCreated", new
            {
                rows = executionPlan.ArtifactsFormatted?.Count ?? 0,
                columns = executionPlan.ArtifactsFormatted?.FirstOrDefault()?.Count ?? 0,
                formula = executionPlan.Formula
            });

            try
            {
                // Step 8: Execute the formula
                string formulaResult;
                object? formulaValue = null;

                try
                {
                    // Find a cell to place the formula (after the data)
                    int formulaRow = executionPlan.ArtifactsFormatted?.Count ?? 0;
                    var formulaCell = dynamicSheet.Cells[formulaRow + 1, 0];

                    // Set the formula
                    formulaCell.Formula = executionPlan.Formula;

                    // Calculate the workbook
                    dynamicWorkbook.CalculateFormula();

                    // Get the result
                    formulaValue = formulaCell.Value;
                    formulaResult = formulaValue?.ToString() ?? "No result";
                }
                catch (Exception formulaEx)
                {
                    _logger.LogWarning(formulaEx, "Formula execution failed, attempting alternative approach");
                    formulaResult = $"Formula error: {formulaEx.Message}";
                }

                // Step 9: Create final result
                var finalResult = new
                {
                    Success = true,
                    Query = query,
                    Answer = formulaResult,
                    Formula = executionPlan.Formula,
                    Reasoning = executionPlan.Reasoning,
                    RequiredCalculation = true,
                    DatasetContext = new
                    {
                        HeaderRowIndex = metadata.DataStartRow - 1,
                        DataStartRow = metadata.DataStartRow,
                        DataEndRow = metadata.TotalRows - 1,
                        TotalDataRows = metadata.DataRowCount,
                        Explanation =
                            $"The dataset has headers at row {metadata.DataStartRow - 1} and contains {metadata.DataRowCount} data rows (from row {metadata.DataStartRow} to {metadata.TotalRows - 1})"
                    },
                    DataUsed = new
                    {
                        Rows = executionPlan.ArtifactsFormatted?.Count ?? 0,
                        Columns = executionPlan.ArtifactsFormatted?.FirstOrDefault()?.Count ?? 0,
                        Headers = executionPlan.ArtifactsFormatted?.FirstOrDefault()
                    }
                };

                await _activityPublisher.PublishAsync("query_spreadsheet.completed", new
                {
                    filePath,
                    query,
                    result = finalResult,
                    success = true,
                    executionTime = DateTime.UtcNow,
                    formulaExecuted = true,
                    formulaResult
                });

                PublishTool("query_spreadsheet", new { filePath, query, sheetName },
                    JsonSerializer.Serialize(finalResult));

                return JsonSerializer.Serialize(finalResult);
            }
            finally
            {
                // Cleanup dynamic workbook
                dynamicWorkbook.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QuerySpreadsheet V3 Refactored failed");

            await _activityPublisher.PublishAsync("query_spreadsheet.error", new
            {
                filePath,
                query,
                error = ex.Message,
                stackTrace = ex.StackTrace
            });

            var errorResult = new
            {
                Success = false,
                Error = ex.Message,
                Query = query
            };

            // Publish tool invocation even for errors
            PublishTool("query_spreadsheet", new { filePath, query, sheetName },
                JsonSerializer.Serialize(errorResult));

            return JsonSerializer.Serialize(errorResult);
        }
    }

    #region Additional Plugin Functions

    // [KernelFunction("analyze_spreadsheet_structure")]
    // [Description("Analyzes the structure and content patterns of an Excel workbook")]
    // public async Task<string> AnalyzeSpreadsheetStructureAsync(
    //     [Description("Full path of the workbook")]
    //     string filePath,
    //     [Description("Level of detail (basic, detailed, comprehensive)")]
    //     string detailLevel = "basic")
    // {
    //     _logger.LogInformation("AnalyzeSpreadsheetStructure: {File} - Level: {Level}", filePath, detailLevel);
    //
    //     try
    //     {
    //         using var workbook = await LoadWorkbookAsync(filePath);
    //
    //         // Analyze each sheet in the workbook to get comprehensive structure
    //         var sheetsInfo = new Dictionary<string, object>();
    //         var allPatterns = new List<object>();
    //         var columnStats = new Dictionary<string, object>();
    //         var totalRows = 0;
    //         var totalColumns = 0;
    //
    //         foreach (Worksheet sheet in workbook.Worksheets)
    //         {
    //             // Analyze sheet structure
    //             var headers = _analysisService.ExtractHeaders(sheet);
    //             var rowCount = sheet.Cells.MaxRow + 1;
    //             var columnCount = sheet.Cells.MaxColumn + 1;
    //             totalRows += rowCount;
    //             totalColumns = Math.Max(totalColumns, columnCount);
    //
    //             sheetsInfo[sheet.Name] = new
    //             {
    //                 RowCount = rowCount,
    //                 ColumnCount = columnCount,
    //                 Headers = headers,
    //                 HasFormulas = sheet.Cells.Cast<Cell>()
    //                     .Take(100)
    //                     .Any(c => !string.IsNullOrEmpty(c.Formula))
    //             };
    //         }
    //
    //         object? result = null;
    //         if (detailLevel.ToLower() == "comprehensive")
    //             result = new
    //             {
    //                 Success = true,
    //                 TotalSheets = sheetsInfo.Count,
    //                 TotalRows = totalRows,
    //                 TotalColumns = totalColumns,
    //                 Sheets = sheetsInfo,
    //                 Message = "Comprehensive analysis without full document context traversal"
    //             };
    //         else if (detailLevel.ToLower() == "detailed")
    //             result = new
    //             {
    //                 Success = true,
    //                 TotalSheets = sheetsInfo.Count,
    //                 TotalRows = totalRows,
    //                 TotalColumns = totalColumns,
    //                 Sheets = sheetsInfo
    //             };
    //         else
    //             result = new
    //             {
    //                 Success = true,
    //                 TotalSheets = sheetsInfo.Count,
    //                 TotalRows = totalRows,
    //                 TotalColumns = totalColumns,
    //                 SheetNames = sheetsInfo.Keys.ToList()
    //             };
    //
    //         PublishTool("analyze_spreadsheet_structure", new { filePath, detailLevel },
    //             JsonSerializer.Serialize(result));
    //
    //         return JsonSerializer.Serialize(result);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "AnalyzeSpreadsheetStructure failed");
    //
    //         var errorResult = new
    //         {
    //             Success = false,
    //             Error = ex.Message
    //         };
    //
    //         PublishTool("analyze_spreadsheet_structure", new { filePath, detailLevel },
    //             JsonSerializer.Serialize(errorResult));
    //
    //         return JsonSerializer.Serialize(errorResult);
    //     }
    // }
    //
    // [KernelFunction("get_spreadsheet_summary")]
    // [Description("Gets a quick summary of spreadsheet contents")]
    // public async Task<string> GetSpreadsheetSummaryAsync(
    //     [Description("Full path of the workbook")]
    //     string filePath)
    // {
    //     _logger.LogInformation("GetSpreadsheetSummary: {File}", filePath);
    //
    //     try
    //     {
    //         using var workbook = await LoadWorkbookAsync(filePath);
    //
    //         var summary = new
    //         {
    //             Success = true,
    //             FileName = Path.GetFileName(filePath),
    //             SheetCount = workbook.Worksheets.Count,
    //             Sheets = workbook.Worksheets.Cast<Worksheet>().Select(sheet => new
    //             {
    //                 Name = sheet.Name,
    //                 RowCount = sheet.Cells.MaxRow + 1,
    //                 ColumnCount = sheet.Cells.MaxColumn + 1,
    //                 Headers = _analysisService.ExtractHeaders(sheet),
    //                 HasFormulas = sheet.Cells.Cast<Cell>()
    //                     .Take(100) // Check first 100 cells for performance
    //                     .Any(c => !string.IsNullOrEmpty(c.Formula))
    //             }).ToList()
    //         };
    //
    //         PublishTool("get_spreadsheet_summary", new { filePath },
    //             JsonSerializer.Serialize(summary));
    //
    //         return JsonSerializer.Serialize(summary);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "GetSpreadsheetSummary failed");
    //
    //         var errorResult = new
    //         {
    //             Success = false,
    //             Error = ex.Message
    //         };
    //
    //         PublishTool("get_spreadsheet_summary", new { filePath },
    //             JsonSerializer.Serialize(errorResult));
    //
    //         return JsonSerializer.Serialize(errorResult);
    //     }
    // }

    #endregion

    #region Private Helper Methods

    private async Task<Workbook> LoadWorkbookAsync(string path)
    {
        Stream stream;
        try
        {
            stream = await _fileStorage.GetFileAsync(Path.GetFileName(path));
        }
        catch (FileNotFoundException)
        {
            stream = File.OpenRead(path);
        }

        return new Workbook(stream);
    }

    private Worksheet GetWorksheet(Workbook workbook, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return workbook.Worksheets[0];

        return workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? workbook.Worksheets[0];
    }

    private object EnrichResultWithContext(
        QueryExecutionResult executionResult,
        QueryAnalysisResult analysisResult,
        SandboxContext sandboxContext)
    {
        return new
        {
            executionResult.Success,
            executionResult.Query,
            executionResult.Value,
            executionResult.Formula,
            executionResult.Explanation,
            executionResult.Error,
            executionResult.Confidence,
            executionResult.ExecutionStrategy,
            Metadata = new Dictionary<string, object>(executionResult.Metadata)
            {
                ["originalRowCount"] = sandboxContext.OriginalRowCount,
                ["processedRowCount"] = sandboxContext.FilteredRowCount,
                ["fullDatasetUsed"] = sandboxContext.FullDatasetPreserved,
                ["userIntentWithContext"] = analysisResult.UserIntentWithContext,
                ["contextSnapshotsCount"] = analysisResult.ContextSnapshots.Count,
                ["artifactSize"] = analysisResult.Artifact?.Length ?? 0
            },
            Context = new
            {
                SandboxInfo = new
                {
                    sandboxContext.SandboxName,
                    sandboxContext.CreatedAt,
                    sandboxContext.AppliedFilters
                },
                AnalysisInfo = new
                {
                    UserIntent = analysisResult.UserIntentWithContext,
                    ColumnsUsed = analysisResult.ColumnsNeeded,
                    AggregationType = analysisResult.AggregationType,
                    RequiredCalculation = analysisResult.RequiresCalculation
                }
            }
        };
    }

    private void PublishTool(string toolName, object parameters, string result)
    {
        try
        {
            _activityPublisher.PublishAsync("tool_invocation", new
            {
                tool = toolName,
                parameters,
                result
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish tool invocation");
        }
    }

    #endregion

    #region DTOs

    /// <summary>
    /// DTO for the execution plan from analysis
    /// </summary>
    private class ExecutionPlanDto
    {
        public bool NeedRunFormula { get; set; }
        public List<List<object>>? ArtifactsFormatted { get; set; }
        public string Formula { get; set; } = "";
        public string SimpleAnswer { get; set; } = "";
        public string Reasoning { get; set; } = "";
    }

    #endregion
}