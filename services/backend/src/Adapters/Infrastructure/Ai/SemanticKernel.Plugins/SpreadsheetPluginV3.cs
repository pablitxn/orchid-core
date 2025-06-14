using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Aspose.Cells;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;

namespace Infrastructure.Ai.SemanticKernel.Plugins;

/// <summary>
/// SpreadsheetPluginV3: Enhanced Excel plugin using sandbox sheet approach for accurate calculations
/// </summary>
public sealed class SpreadsheetPluginV3(
    ILogger<SpreadsheetPluginV3> logger,
    IMemoryCache memoryCache,
    IDistributedCache distributedCache,
    IFileStorageService fileStorage,
    IActivityPublisher activityPublisher,
    IChatCompletionService chatCompletion)
{
    private readonly ILogger<SpreadsheetPluginV3> _log = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IMemoryCache _mem = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

    private readonly IDistributedCache _redis =
        distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));

    private readonly IFileStorageService _storage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));

    private readonly IActivityPublisher _activity =
        activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));

    private readonly IChatCompletionService _chat =
        chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));

    private const int MaxSandboxRows = 50000;
    private const int MaxRetries = 5;

    #region Structured Output Models

    private sealed class AnalysisResponse
    {
        [JsonPropertyName("columns_needed")] public List<string> ColumnsNeeded { get; set; } = new();
        [JsonPropertyName("filters")] public List<FilterCriteria> Filters { get; set; } = new();
        [JsonPropertyName("aggregation_type")] public string AggregationType { get; set; } = "";
        [JsonPropertyName("group_by")] public string? GroupBy { get; set; }

        [JsonPropertyName("requires_calculation")]
        public bool RequiresCalculation { get; set; }

        [JsonPropertyName("calculation_steps")]
        public List<string> CalculationSteps { get; set; } = new();
    }

    private sealed class FilterCriteria
    {
        [JsonPropertyName("column")] public string Column { get; set; } = "";
        [JsonPropertyName("operator")] public string Operator { get; set; } = "";
        [JsonPropertyName("value")] public string Value { get; set; } = "";
    }

    private sealed class FormulaStrategy
    {
        [JsonPropertyName("approach")] public string Approach { get; set; } = "";
        [JsonPropertyName("formula")] public string Formula { get; set; } = "";
        [JsonPropertyName("helper_columns")] public List<HelperColumn> HelperColumns { get; set; } = new();
        [JsonPropertyName("explanation")] public string Explanation { get; set; } = "";
    }

    private sealed class HelperColumn
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("formula")] public string Formula { get; set; } = "";
        [JsonPropertyName("purpose")] public string Purpose { get; set; } = "";
    }

    // New context model to preserve original dataset information
    private sealed class SandboxContext
    {
        public int OriginalRowCount { get; set; }
        public int FilteredRowCount { get; set; }
        public Dictionary<string, int> ColumnStats { get; set; } = new();
        public List<FilterCriteria> AppliedFilters { get; set; } = new();
        public bool FullDatasetPreserved { get; set; }
    }

    #endregion

    #region Main Entry Points

    [KernelFunction("query_spreadsheet")]
    [Description("Queries Excel data using natural language with high accuracy through sandbox execution")]
    public async Task<string> QuerySpreadsheetAsync(
        [Description("Full path of the workbook")]
        string filePath,
        [Description("Natural language query")]
        string query,
        [Description("Sheet name (optional)")] string sheetName = "")
    {
        _log.LogInformation("QuerySpreadsheet V3: {File} - '{Query}'", filePath, query);

        // Log the start of the query process
        await _activity.PublishAsync("query_spreadsheet.start", new
        {
            filePath,
            query,
            sheetName,
            timestamp = DateTime.UtcNow
        });

        try
        {
            // Load workbook
            using var wb = await LoadWorkbookAsync(filePath);
            var originalSheet = GetWorksheet(wb, sheetName);

            // Log sheet info
            await _activity.PublishAsync("query_spreadsheet.sheet_loaded", new
            {
                filePath,
                sheetName = originalSheet.Name,
                rowCount = originalSheet.Cells.MaxRow + 1,
                columnCount = originalSheet.Cells.MaxColumn + 1,
                headers = GetHeaders(originalSheet)
            });

            // Analyze the query
            var analysis = await AnalyzeQueryRequirementsAsync(query, originalSheet);

            // Create sandbox environment with context
            var (sandbox, context) = await CreateSandboxSheetWithContextAsync(wb, originalSheet, analysis);

            // Log sandbox creation
            await _activity.PublishAsync("query_spreadsheet.sandbox_created", new
            {
                sandboxName = sandbox.Name,
                rowCount = CountDataRows(sandbox) + 1,
                columnCount = GetHeaders(sandbox).Count,
                filtersApplied = analysis.Filters,
                originalRowCount = context.OriginalRowCount,
                filteredRowCount = context.FilteredRowCount,
                fullDatasetPreserved = context.FullDatasetPreserved
            });

            try
            {
                // Execute query with smart retries
                var result = await ExecuteWithSmartRetriesAsync(wb, sandbox, context, query, analysis);

                // Log final result
                await _activity.PublishAsync("query_spreadsheet.completed", new
                {
                    filePath,
                    query,
                    result,
                    sandboxName = sandbox.Name,
                    success = result.Success,
                    executionTime = DateTime.UtcNow
                });

                return JsonSerializer.Serialize(result);
            }
            finally
            {
                // Cleanup sandbox
                CleanupSandbox(wb, sandbox);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "QuerySpreadsheet V3 failed");

            await _activity.PublishAsync("query_spreadsheet.error", new
            {
                filePath,
                query,
                error = ex.Message,
                stackTrace = ex.StackTrace
            });

            return JsonSerializer.Serialize(new QueryResult
            {
                Success = false,
                Error = ex.Message,
                Query = query
            });
        }
    }

    #endregion

    #region Query Analysis

    private async Task<AnalysisResponse> AnalyzeQueryRequirementsAsync(string query, Worksheet sheet)
    {
        // Get column headers
        var headers = GetHeaders(sheet);

        var prompt = $"""
                      Analyze this Excel query to determine what columns and operations are needed.

                      Query: {query}

                      Available columns: {string.Join(", ", headers)}

                      Determine:
                      1. Which columns are needed for the calculation
                      2. Any filters to apply (column, operator, value)
                      3. The aggregation type (sum, average, count, max, min, percentile, variance, etc.)
                      4. If grouping is needed, which column to group by
                      5. If complex calculations are needed beyond simple aggregation

                      For filters:
                      - Use 'equals' for exact matches
                      - Use 'contains' for partial text matches
                      - Use comparison operators for numbers: >, <, >=, <=

                      For aggregation types, use exact Excel function names when possible.
                      
                      IMPORTANT: For percentage queries (e.g., "What percentage of rows have X > Y"), the filters should identify the condition, 
                      but the calculation will need to compare against the total dataset, not just filtered rows.
                      """;

        var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "query_analysis",
            jsonSchema: BinaryData.FromString("""
                                              {
                                                  "type": "object",
                                                  "properties": {
                                                      "columns_needed": {
                                                          "type": "array",
                                                          "items": { "type": "string" }
                                                      },
                                                      "filters": {
                                                          "type": "array",
                                                          "items": {
                                                              "type": "object",
                                                              "properties": {
                                                                  "column": { "type": "string" },
                                                                  "operator": { "type": "string" },
                                                                  "value": { "type": "string" }
                                                              },
                                                              "required": ["column", "operator", "value"],
                                                              "additionalProperties": false
                                                          }
                                                      },
                                                      "aggregation_type": { "type": "string" },
                                                      "group_by": { "type": ["string", "null"] },
                                                      "requires_calculation": { "type": "boolean" },
                                                      "calculation_steps": {
                                                          "type": "array",
                                                          "items": { "type": "string" }
                                                      }
                                                  },
                                                  "required": ["columns_needed", "filters", "aggregation_type", "requires_calculation", "calculation_steps", "group_by"],
                                                  "additionalProperties": false
                                              }
                                              """),
            jsonSchemaIsStrict: true
        );

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = "o4-mini",
            ResponseFormat = responseFormat,
            Temperature = 0.1
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, prompt);

        // Log LLM call
        await _activity.PublishAsync("query_spreadsheet.AnalyzeQueryRequirementsAsync", new
        {
            step = "llm_call",
            model = settings.ModelId,
            prompt,
            availableColumns = headers,
            temperature = settings.Temperature
        });

        var response = await _chat.GetChatMessageContentsAsync(chatHistory, settings);
        var analysisResponse = JsonSerializer.Deserialize<AnalysisResponse>(response[0].Content ?? "{}") ??
                               new AnalysisResponse();

        // Log LLM response
        await _activity.PublishAsync("query_spreadsheet.AnalyzeQueryRequirementsAsync", new
        {
            step = "llm_response",
            response = analysisResponse,
            rawResponse = response[0].Content
        });

        return analysisResponse;
    }

    #endregion

    #region Sandbox Management

    private async Task<(Worksheet sandbox, SandboxContext context)> CreateSandboxSheetWithContextAsync(
        Workbook workbook, 
        Worksheet originalSheet,
        AnalysisResponse analysis)
    {
        var context = new SandboxContext();
        var sandboxName = $"_sandbox_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var sandbox = workbook.Worksheets.Add(sandboxName);

        var headers = GetHeaders(originalSheet);
        var dataRange = GetDataRange(originalSheet);
        
        // IMPORTANT: Save the total count BEFORE filtering
        context.OriginalRowCount = dataRange.LastRow - dataRange.FirstRow + 1;

        // Copy headers
        for (int col = 0; col < headers.Count; col++)
        {
            sandbox.Cells[0, col].Value = headers[col];
        }

        // Determine if we need to preserve all data for percentage calculations
        bool needsFullDataset = QueryNeedsFullDataset(analysis);
        
        if (needsFullDataset)
        {
            // For percentage queries, copy ALL data
            await CopyAllDataAsync(originalSheet, sandbox, headers, dataRange);
            context.FilteredRowCount = context.OriginalRowCount;
            context.FullDatasetPreserved = true;
            
            _log.LogInformation("Preserved full dataset for percentage calculation: {Rows} rows", context.OriginalRowCount);
        }
        else
        {
            // For other queries, apply filters normally
            context.FilteredRowCount = await CopyFilteredDataAsync(
                originalSheet, sandbox, headers, dataRange, analysis.Filters);
            context.FullDatasetPreserved = false;
            
            _log.LogInformation("Created filtered sandbox: {FilteredRows} of {TotalRows} rows", 
                context.FilteredRowCount, context.OriginalRowCount);
        }

        context.AppliedFilters = analysis.Filters;
        
        // Calculate column statistics if needed
        await CalculateColumnStatsAsync(sandbox, headers, context);

        // Add helper columns if needed
        await AddHelperColumnsAsync(sandbox, headers.Count, analysis);

        return (sandbox, context);
    }

    private bool QueryNeedsFullDataset(AnalysisResponse analysis)
    {
        // Detect if the query needs the full dataset
        return analysis.CalculationSteps.Any(step => 
            step.Contains("percentage", StringComparison.OrdinalIgnoreCase) ||
            step.Contains("proportion", StringComparison.OrdinalIgnoreCase) ||
            step.Contains("percent", StringComparison.OrdinalIgnoreCase) ||
            step.Contains("total", StringComparison.OrdinalIgnoreCase) ||
            step.Contains("all rows", StringComparison.OrdinalIgnoreCase));
    }

    private async Task CopyAllDataAsync(Worksheet source, Worksheet target, List<string> headers, (int FirstRow, int LastRow) dataRange)
    {
        await Task.CompletedTask;
        
        int targetRow = 1;
        for (int row = dataRange.FirstRow; row <= dataRange.LastRow && targetRow < MaxSandboxRows; row++)
        {
            for (int col = 0; col < headers.Count; col++)
            {
                var value = source.Cells[row, col].Value;
                target.Cells[targetRow, col].Value = value;

                // Preserve number format for better calculation
                if (value != null && CellValueParser.TryParseNumeric(value, out var numValue))
                {
                    target.Cells[targetRow, col].PutValue(numValue);
                }
            }
            targetRow++;
        }
    }

    private async Task<int> CopyFilteredDataAsync(
        Worksheet source, 
        Worksheet target, 
        List<string> headers, 
        (int FirstRow, int LastRow) dataRange,
        List<FilterCriteria> filters)
    {
        await Task.CompletedTask;
        
        int targetRow = 1;
        int filteredOutCount = 0;

        for (int row = dataRange.FirstRow; row <= dataRange.LastRow && targetRow < MaxSandboxRows; row++)
        {
            if (RowMatchesFiltersImproved(source, row, headers, filters))
            {
                for (int col = 0; col < headers.Count; col++)
                {
                    var value = source.Cells[row, col].Value;
                    target.Cells[targetRow, col].Value = value;

                    // Preserve number format for better calculation
                    if (value != null && CellValueParser.TryParseNumeric(value, out var numValue))
                    {
                        target.Cells[targetRow, col].PutValue(numValue);
                    }
                }
                targetRow++;
            }
            else
            {
                filteredOutCount++;
            }
        }

        _log.LogInformation("Filtered out {FilteredOut} rows", filteredOutCount);
        return targetRow - 1;
    }

    private async Task CalculateColumnStatsAsync(Worksheet sandbox, List<string> headers, SandboxContext context)
    {
        await Task.CompletedTask;
        
        // Calculate basic statistics for numeric columns if needed
        var dataRows = CountDataRows(sandbox);
        
        for (int col = 0; col < headers.Count; col++)
        {
            int numericCount = 0;
            for (int row = 1; row <= dataRows; row++)
            {
                if (CellValueParser.TryParseNumeric(sandbox.Cells[row, col].Value, out _))
                {
                    numericCount++;
                }
            }
            
            if (numericCount > 0)
            {
                context.ColumnStats[headers[col]] = numericCount;
            }
        }
    }

    private async Task AddHelperColumnsAsync(Worksheet sandbox, int startCol, AnalysisResponse analysis)
    {
        await Task.CompletedTask;

        // Add helper columns for complex calculations if needed
        if (analysis.RequiresCalculation && analysis.CalculationSteps.Any())
        {
            int helperCol = startCol;

            // Example: Add a helper column for ratio calculations
            if (analysis.CalculationSteps.Any(s => s.Contains("ratio", StringComparison.OrdinalIgnoreCase)))
            {
                sandbox.Cells[0, helperCol].Value = "_Helper_Ratio";
                // Formula will be added during execution
                helperCol++;
            }
        }
    }

    private void CleanupSandbox(Workbook workbook, Worksheet sandbox)
    {
        try
        {
            var index = workbook.Worksheets.IndexOf(sandbox);
            if (index >= 0)
            {
                workbook.Worksheets.RemoveAt(index);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to cleanup sandbox sheet");
        }
    }

    #endregion

    #region Smart Retry System

    private async Task<QueryResult> ExecuteWithSmartRetriesAsync(
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        AnalysisResponse analysis)
    {
        var strategies = new List<(string Name, Func<Task<QueryResult>>)>
        {
            // Strategy 1: Standard Excel formula
            ("Standard Formula", async () => await ExecuteQueryInSandboxAsync(workbook, sandbox, context, query, analysis)),
            
            // Strategy 2: Formula with helper columns
            ("Helper Columns", async () => await ExecuteWithHelperColumnsStrategyAsync(workbook, sandbox, context, query, analysis)),
            
            // Strategy 3: Manual calculation
            ("Manual Calculation", async () => await CalculateManuallyAsync(sandbox, query, analysis, context)),
            
            // Strategy 4: Split into sub-queries
            ("Sub-queries", async () => await ExecuteAsSubqueriesAsync(workbook, sandbox, context, query, analysis))
        };

        QueryResult? bestResult = null;
        var errors = new List<string>();

        foreach (var (strategyName, strategy) in strategies)
        {
            try
            {
                await _activity.PublishAsync("query_spreadsheet.strategy_attempt", new
                {
                    strategy = strategyName,
                    query
                });

                var result = await strategy();
                
                if (result.Success)
                {
                    // Validate the result
                    var validated = await ValidateAndCorrectResultAsync(result, context, analysis, query);
                    
                    if (validated.Success && (bestResult == null || validated.Confidence > bestResult.Confidence))
                    {
                        bestResult = validated;
                        
                        // If we have a high-confidence result, we can stop trying
                        if (validated.Confidence >= 0.9)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    errors.Add($"{strategyName}: {result.Error ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{strategyName}: {ex.Message}");
                _log.LogWarning(ex, "Strategy {Strategy} failed", strategyName);
            }
        }

        return bestResult ?? new QueryResult
        {
            Query = query,
            Success = false,
            Error = $"All strategies failed: {string.Join("; ", errors)}"
        };
    }

    #endregion

    #region Query Execution

    private async Task<QueryResult> ExecuteQueryInSandboxAsync(
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        AnalysisResponse analysis)
    {
        var result = new QueryResult { Query = query };

        try
        {
            // Determine formula strategy with context awareness
            var strategy = await DetermineContextAwareFormulaStrategyAsync(sandbox, context, query, analysis);

            // Try multiple approaches if needed
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                await _activity.PublishAsync("query_spreadsheet.execution_attempt", new
                {
                    attempt,
                    strategy = strategy.Approach,
                    formula = strategy.Formula
                });

                try
                {
                    // Apply helper columns
                    ApplyHelperFormulas(sandbox, strategy);

                    // Execute main formula
                    var formulaResult = await ExecuteFormulaAsync(workbook, sandbox, strategy.Formula);

                    if (formulaResult.Success)
                    {
                        result.Success = true;
                        result.Value = formulaResult.Value;
                        result.Formula = strategy.Formula;
                        result.Explanation = strategy.Explanation;
                        result.Confidence = CalculateConfidence(attempt, analysis);
                        break;
                    }

                    // If failed, try alternative approach
                    _log.LogWarning("Attempt {Attempt} failed: {Error}", attempt, formulaResult.Error);

                    await _activity.PublishAsync("query_spreadsheet.execution_failed", new
                    {
                        attempt,
                        error = formulaResult.Error,
                        formula = strategy.Formula
                    });

                    strategy = await RefineStrategyAsync(strategy, formulaResult.Error, sandbox, analysis);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Execution attempt {Attempt} failed", attempt);

                    if (attempt == MaxRetries)
                    {
                        result.Error = ex.Message;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<FormulaStrategy> DetermineContextAwareFormulaStrategyAsync(
        Worksheet sandbox,
        SandboxContext context,
        string query,
        AnalysisResponse analysis)
    {
        var headers = GetHeaders(sandbox);
        var dataRows = CountDataRows(sandbox);

        // Include context information in the prompt
        var prompt = $"""
                      Generate an Excel formula strategy for this query.

                      Query: {query}

                      Sandbox sheet structure:
                      - Headers at row 1: {string.Join(", ", headers)}
                      - Data rows: 2 to {dataRows + 1}
                      - Total rows in sandbox: {dataRows}
                      - Original dataset size: {context.OriginalRowCount} rows
                      - Filters applied: {JsonSerializer.Serialize(context.AppliedFilters)}
                      - Full dataset preserved: {context.FullDatasetPreserved}

                      Required operation: {analysis.AggregationType}
                      {(analysis.GroupBy != null ? $"Group by: {analysis.GroupBy}" : "")}

                      CRITICAL INSTRUCTIONS FOR PERCENTAGE CALCULATIONS:
                      - If calculating percentage of rows matching a condition:
                        * If the sandbox contains ALL data ({context.FullDatasetPreserved}): Use COUNTIF for condition / COUNTA for total
                        * The filters from analysis are the CONDITIONS to check, not pre-applied filters
                      - For percentage queries, the formula should count rows matching the condition vs total rows

                      Important:
                      - Use absolute references for ranges (e.g., $A$2:$A$100)
                      - For the sandbox data range, use rows 2 to {dataRows + 1}
                      - Handle edge cases (division by zero, empty ranges)
                      - If multiple steps are needed, describe helper columns

                      Provide a formula that will work in cell Z1 of the sandbox sheet.
                      """;

        var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "formula_strategy",
            jsonSchema: BinaryData.FromString("""
                                              {
                                                  "type": "object",
                                                  "properties": {
                                                      "approach": { "type": "string" },
                                                      "formula": { "type": "string" },
                                                      "helper_columns": {
                                                          "type": "array",
                                                          "items": {
                                                              "type": "object",
                                                              "properties": {
                                                                  "name": { "type": "string" },
                                                                  "formula": { "type": "string" },
                                                                  "purpose": { "type": "string" }
                                                              },
                                                              "required": ["name", "formula", "purpose"],
                                                              "additionalProperties": false
                                                          }
                                                      },
                                                      "explanation": { "type": "string" }
                                                  },
                                                  "required": ["approach", "formula", "explanation", "helper_columns"],
                                                  "additionalProperties": false
                                              }
                                              """),
            jsonSchemaIsStrict: true
        );

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = "o4-mini",
            ResponseFormat = responseFormat,
            Temperature = 0.1
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, prompt);

        // Log LLM call
        await _activity.PublishAsync("query_spreadsheet.DetermineFormulaStrategyAsync", new
        {
            step = "llm_call",
            model = settings.ModelId,
            prompt,
            sheetStructure = new
            {
                headers,
                dataRows,
                aggregationType = analysis.AggregationType,
                groupBy = analysis.GroupBy,
                context = new
                {
                    context.OriginalRowCount,
                    context.FilteredRowCount,
                    context.FullDatasetPreserved
                }
            }
        });

        var response = await _chat.GetChatMessageContentsAsync(chatHistory, settings);
        var strategy = JsonSerializer.Deserialize<FormulaStrategy>(response[0].Content ?? "{}") ??
                       new FormulaStrategy();

        // Log LLM response
        await _activity.PublishAsync("query_spreadsheet.DetermineFormulaStrategyAsync", new
        {
            step = "llm_response",
            strategy,
            rawResponse = response[0].Content
        });

        return strategy;
    }

    private void ApplyHelperFormulas(Worksheet sandbox, FormulaStrategy strategy)
    {
        if (!strategy.HelperColumns.Any()) return;

        var headers = GetHeaders(sandbox);
        var dataRows = CountDataRows(sandbox);
        int nextCol = headers.Count;

        foreach (var helper in strategy.HelperColumns)
        {
            // Add header
            sandbox.Cells[0, nextCol].Value = helper.Name;

            // Apply formula to all data rows
            for (int row = 1; row <= dataRows; row++)
            {
                var adjustedFormula = helper.Formula.Replace("2", (row + 1).ToString());
                sandbox.Cells[row, nextCol].Formula = adjustedFormula;
            }

            nextCol++;
        }
    }

    private async Task<FormulaExecutionResult> ExecuteFormulaAsync(Workbook workbook, Worksheet sandbox, string formula)
    {
        await Task.CompletedTask;

        var result = new FormulaExecutionResult();

        try
        {
            // Place formula in a specific cell
            var resultCell = sandbox.Cells["Z1"];
            resultCell.Formula = formula;

            // Calculate
            workbook.CalculateFormula();

            // Get result
            var value = resultCell.Value;
            var stringValue = resultCell.StringValue;

            // Log formula execution
            await _activity.PublishAsync("query_spreadsheet.sandbox_formula_execution", new
            {
                formula,
                cell = "Z1",
                value,
                stringValue,
                success = !stringValue.StartsWith("#")
            });

            if (stringValue.StartsWith("#"))
            {
                result.Success = false;
                result.Error = $"Excel error: {stringValue}";
            }
            else
            {
                result.Success = true;
                result.Value = value;
                result.FormattedValue = stringValue;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;

            await _activity.PublishAsync("query_spreadsheet.sandbox_formula_error", new
            {
                formula,
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }

        return result;
    }

    private async Task<FormulaStrategy> RefineStrategyAsync(
        FormulaStrategy currentStrategy,
        string error,
        Worksheet sandbox,
        AnalysisResponse analysis)
    {
        var headers = GetHeaders(sandbox);
        var dataRows = CountDataRows(sandbox);

        var prompt = $"""
                      The previous formula failed. Please provide an alternative approach.

                      Failed formula: {currentStrategy.Formula}
                      Error: {error}

                      Sheet structure:
                      - Headers: {string.Join(", ", headers)}
                      - Data rows: 2 to {dataRows + 1}

                      Common issues and solutions:
                      - #N/A: VLOOKUP/MATCH not finding values - try SUMIF/COUNTIF instead
                      - #DIV/0: Division by zero - add IFERROR or check denominator
                      - #VALUE: Type mismatch - ensure proper data type conversion
                      - Range issues: Verify row numbers match actual data

                      Provide a more robust formula that handles edge cases.
                      """;

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = "o4-mini",
            Temperature = 0.2
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, prompt);

        // Log LLM call
        await _activity.PublishAsync("query_spreadsheet.RefineStrategyAsync", new
        {
            step = "llm_call",
            model = settings.ModelId,
            prompt,
            previousFormula = currentStrategy.Formula,
            error
        });

        var response = await _chat.GetChatMessageContentsAsync(chatHistory, settings);

        // Parse response manually for refinement
        var newStrategy = new FormulaStrategy
        {
            Approach = "Refined approach",
            Formula = ExtractFormula(response[0].Content ?? ""),
            Explanation = $"Refined after error: {error}"
        };

        // Log LLM response
        await _activity.PublishAsync("query_spreadsheet.RefineStrategyAsync", new
        {
            step = "llm_response",
            newStrategy,
            rawResponse = response[0].Content
        });

        return newStrategy;
    }

    #endregion

    #region Additional Strategies

    private async Task<QueryResult> ExecuteWithHelperColumnsStrategyAsync(
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        AnalysisResponse analysis)
    {
        // Implementation for helper columns strategy
        // This would be similar to the main execution but with more complex helper formulas
        return await ExecuteQueryInSandboxAsync(workbook, sandbox, context, query, analysis);
    }

    private async Task<QueryResult> CalculateManuallyAsync(
        Worksheet sheet,
        string query,
        AnalysisResponse analysis,
        SandboxContext context)
    {
        var result = new QueryResult { Query = query };
        
        try
        {
            var headers = GetHeaders(sheet);
            var dataRange = GetDataRange(sheet);
            
            // For percentage queries with filters
            if (IsPercentageQuery(query) && analysis.Filters.Any())
            {
                int totalCount = 0;
                int matchingCount = 0;
                
                // Count in the full dataset
                for (int row = dataRange.FirstRow; row <= dataRange.LastRow; row++)
                {
                    totalCount++;
                    
                    if (RowMatchesFiltersImproved(sheet, row, headers, analysis.Filters))
                    {
                        matchingCount++;
                    }
                }
                
                result.Success = true;
                result.Value = totalCount > 0 ? (matchingCount * 100.0 / totalCount) : 0;
                result.Explanation = $"Manual calculation: {matchingCount} of {totalCount} rows match the criteria ({result.Value:F2}%)";
                result.Confidence = 0.95;
            }
            else
            {
                // Other types of calculations
                var columnIndex = headers.FindIndex(h =>
                    analysis.ColumnsNeeded.Any(c => c.Equals(h, StringComparison.OrdinalIgnoreCase)));

                if (columnIndex < 0)
                {
                    result.Error = "Required column not found";
                    return result;
                }

                // Manual calculation based on aggregation type
                var values = new List<double>();
                var dataRows = CountDataRows(sheet);

                for (int row = 1; row <= dataRows; row++)
                {
                    var cellValue = sheet.Cells[row, columnIndex].Value;
                    if (CellValueParser.TryParseNumeric(cellValue, out var numValue))
                    {
                        values.Add(numValue);
                    }
                }

                if (!values.Any())
                {
                    result.Error = "No numeric values found";
                    return result;
                }

                // Calculate based on aggregation type
                result.Success = true;
                result.Value = analysis.AggregationType.ToLower() switch
                {
                    "sum" => values.Sum(),
                    "average" or "avg" => values.Average(),
                    "max" => values.Max(),
                    "min" => values.Min(),
                    "count" => values.Count,
                    "variance" or "var" => CalculateVariance(values),
                    _ => values.Sum()
                };

                result.Explanation = $"Manual calculation: {analysis.AggregationType} of {values.Count} values";
                result.Confidence = 0.85;
            }

            await _activity.PublishAsync("query_spreadsheet.manual_calculation_result", new
            {
                aggregationType = analysis.AggregationType,
                result = result.Value,
                confidence = result.Confidence
            });
        }
        catch (Exception ex)
        {
            result.Error = $"Manual calculation failed: {ex.Message}";
        }
        
        return result;
    }

    private async Task<QueryResult> ExecuteAsSubqueriesAsync(
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        AnalysisResponse analysis)
    {
        // Implementation for breaking complex queries into sub-queries
        // This is a fallback for very complex queries
        return await TryFallbackApproachAsync(workbook, sandbox, query, analysis);
    }

    private async Task<QueryResult> TryFallbackApproachAsync(
        Workbook workbook,
        Worksheet sandbox,
        string query,
        AnalysisResponse analysis)
    {
        _log.LogInformation("Trying fallback approach with manual calculation");

        await _activity.PublishAsync("query_spreadsheet.fallback_approach", new
        {
            reason = "All formula attempts failed",
            aggregationType = analysis.AggregationType
        });

        // For complex queries, break down into simpler steps
        var result = new QueryResult { Query = query };

        try
        {
            var headers = GetHeaders(sandbox);
            var columnIndex = headers.FindIndex(h =>
                analysis.ColumnsNeeded.Any(c => c.Equals(h, StringComparison.OrdinalIgnoreCase)));

            if (columnIndex < 0)
            {
                result.Error = "Required column not found";
                return result;
            }

            // Manual calculation based on aggregation type
            var values = new List<double>();
            var dataRows = CountDataRows(sandbox);

            for (int row = 1; row <= dataRows; row++)
            {
                var cellValue = sandbox.Cells[row, columnIndex].Value;
                if (cellValue != null && double.TryParse(cellValue.ToString(), out var numValue))
                {
                    values.Add(numValue);
                }
            }

            if (!values.Any())
            {
                result.Error = "No numeric values found";
                return result;
            }

            // Calculate based on aggregation type
            result.Success = true;
            result.Value = analysis.AggregationType.ToLower() switch
            {
                "sum" => values.Sum(),
                "average" or "avg" => values.Average(),
                "max" => values.Max(),
                "min" => values.Min(),
                "count" => values.Count,
                "variance" or "var" => CalculateVariance(values),
                _ => values.Sum()
            };

            result.Explanation =
                $"Calculated using fallback method: {analysis.AggregationType} of {values.Count} values";
            result.Confidence = 0.7;

            await _activity.PublishAsync("query_spreadsheet.fallback_result", new
            {
                aggregationType = analysis.AggregationType,
                valueCount = values.Count,
                result = result.Value
            });
        }
        catch (Exception ex)
        {
            result.Error = $"Fallback calculation failed: {ex.Message}";
        }

        return result;
    }

    #endregion

    #region Validation

    private async Task<QueryResult> ValidateAndCorrectResultAsync(
        QueryResult result,
        SandboxContext context,
        AnalysisResponse analysis,
        string query)
    {
        // Validate suspicious results
        if (result.Success && IsPercentageQuery(query))
        {
            var percentage = Convert.ToDouble(result.Value);
            
            // If the result is exactly 100% and there were filters, it's suspicious
            if (Math.Abs(percentage - 100) < 0.001 && context.AppliedFilters.Any())
            {
                await _activity.PublishAsync("query_spreadsheet.suspicious_result", new
                {
                    result = percentage,
                    reason = "100% result with filters applied",
                    originalRows = context.OriginalRowCount,
                    filteredRows = context.FilteredRowCount,
                    fullDatasetPreserved = context.FullDatasetPreserved
                });

                // If the sandbox was pre-filtered, this is definitely wrong
                if (!context.FullDatasetPreserved)
                {
                    result.Success = false;
                    result.Error = "Incorrect percentage calculation due to pre-filtered data";
                    result.Confidence = 0;
                }
                else
                {
                    // Even with full dataset, 100% might be suspicious
                    result.Confidence = Math.Min(result.Confidence, 0.5);
                }
            }
        }

        return result;
    }

    private bool IsPercentageQuery(string query)
    {
        return query.Contains("percentage", StringComparison.OrdinalIgnoreCase) ||
               query.Contains("percent", StringComparison.OrdinalIgnoreCase) ||
               query.Contains("%", StringComparison.OrdinalIgnoreCase) ||
               query.Contains("proportion", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Helper Methods

    private async Task<Workbook> LoadWorkbookAsync(string path)
    {
        Stream stream;
        try
        {
            stream = await _storage.GetFileAsync(Path.GetFileName(path));
        }
        catch (FileNotFoundException)
        {
            stream = File.OpenRead(path);
        }

        return new Workbook(stream);
    }

    private Worksheet GetWorksheet(Workbook wb, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return wb.Worksheets[0];

        return wb.Worksheets.FirstOrDefault(w =>
            w.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? wb.Worksheets[0];
    }

    private List<string> GetHeaders(Worksheet sheet)
    {
        var headers = new List<string>();
        var maxCol = sheet.Cells.MaxColumn;

        for (int col = 0; col <= maxCol; col++)
        {
            var header = sheet.Cells[0, col].StringValue.Trim();
            if (!string.IsNullOrEmpty(header))
            {
                headers.Add(header);
            }
        }

        return headers;
    }

    private (int FirstRow, int LastRow) GetDataRange(Worksheet sheet)
    {
        var maxRow = sheet.Cells.MaxRow;
        return (1, maxRow); // Assuming row 0 is headers
    }

    private int CountDataRows(Worksheet sheet)
    {
        var range = GetDataRange(sheet);
        return range.LastRow - range.FirstRow + 1;
    }

    private bool IsNumeric(object value)
    {
        return value != null && (value is double || value is int || value is decimal ||
                                 double.TryParse(value.ToString(), out _));
    }

    private int TryCompareNumeric(string value1, string value2)
    {
        if (double.TryParse(value1, out var num1) && double.TryParse(value2, out var num2))
        {
            return num1.CompareTo(num2);
        }

        return string.Compare(value1, value2, StringComparison.OrdinalIgnoreCase);
    }

    private double CalculateVariance(List<double> values)
    {
        if (!values.Any()) return 0;

        var mean = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return sumOfSquares / (values.Count - 1); // Sample variance
    }

    private double CalculateConfidence(int attempts, AnalysisResponse analysis)
    {
        var baseConfidence = 1.0 - (attempts - 1) * 0.15;

        // Boost confidence for simpler queries
        if (!analysis.RequiresCalculation && analysis.GroupBy == null)
        {
            baseConfidence += 0.1;
        }

        return Math.Max(0.3, Math.Min(1.0, baseConfidence));
    }

    private string ExtractFormula(string text)
    {
        // Extract formula from AI response
        var formulaMatch = Regex.Match(text, @"=[\w\s\$\(\):,\.\+\-\*/""<>]+", RegexOptions.IgnoreCase);
        return formulaMatch.Success ? formulaMatch.Value : text.Trim();
    }

    private bool RowMatchesFilters(Worksheet sheet, int row, List<string> headers, List<FilterCriteria> filters)
    {
        if (!filters.Any()) return true;

        foreach (var filter in filters)
        {
            var colIndex = headers.FindIndex(h => h.Equals(filter.Column, StringComparison.OrdinalIgnoreCase));
            if (colIndex < 0) continue;

            var cellValue = sheet.Cells[row, colIndex].StringValue;

            bool matches = filter.Operator.ToLower() switch
            {
                "equals" => cellValue.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
                "contains" => cellValue.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
                ">" => TryCompareNumeric(cellValue, filter.Value) > 0,
                "<" => TryCompareNumeric(cellValue, filter.Value) < 0,
                ">=" => TryCompareNumeric(cellValue, filter.Value) >= 0,
                "<=" => TryCompareNumeric(cellValue, filter.Value) <= 0,
                _ => true
            };

            if (!matches) return false;
        }

        return true;
    }

    private bool RowMatchesFiltersImproved(Worksheet sheet, int row, List<string> headers, List<FilterCriteria> filters)
    {
        if (!filters.Any()) return true;

        foreach (var filter in filters)
        {
            var colIndex = headers.FindIndex(h => h.Equals(filter.Column, StringComparison.OrdinalIgnoreCase));
            if (colIndex < 0) continue;

            var cellValue = sheet.Cells[row, colIndex].Value;
            bool matches = false;

            switch (filter.Operator.ToLower())
            {
                case "equals":
                    matches = CompareEquals(cellValue, filter.Value);
                    break;
                    
                case "contains":
                    matches = cellValue?.ToString()?.Contains(filter.Value, StringComparison.OrdinalIgnoreCase) ?? false;
                    break;
                    
                case ">":
                case "<":
                case ">=":
                case "<=":
                    if (CellValueParser.TryParseNumeric(cellValue, out var numVal) &&
                        double.TryParse(filter.Value, out var filterNum))
                    {
                        matches = filter.Operator switch
                        {
                            ">" => numVal > filterNum,
                            "<" => numVal < filterNum,
                            ">=" => numVal >= filterNum,
                            "<=" => numVal <= filterNum,
                            _ => false
                        };
                    }
                    break;
                    
                case "date>":
                case "date<":
                case "date>=":
                case "date<=":
                    var dateVal = CellValueParser.TryParseDate(cellValue);
                    if (dateVal.HasValue && DateTime.TryParse(filter.Value, out var filterDate))
                    {
                        matches = filter.Operator switch
                        {
                            "date>" => dateVal.Value > filterDate,
                            "date<" => dateVal.Value < filterDate,
                            "date>=" => dateVal.Value >= filterDate,
                            "date<=" => dateVal.Value <= filterDate,
                            _ => false
                        };
                    }
                    break;
            }

            if (!matches) return false;
        }

        return true;
    }

    private bool CompareEquals(object cellValue, string filterValue)
    {
        if (cellValue == null) return string.IsNullOrEmpty(filterValue);
        
        var cellStr = cellValue.ToString() ?? "";
        
        // Try numeric comparison first
        if (CellValueParser.TryParseNumeric(cellValue, out var cellNum) && 
            double.TryParse(filterValue, out var filterNum))
        {
            return Math.Abs(cellNum - filterNum) < 0.0001;
        }
        
        // Try date comparison
        var cellDate = CellValueParser.TryParseDate(cellValue);
        if (cellDate.HasValue && DateTime.TryParse(filterValue, out var filterDate))
        {
            return cellDate.Value.Date == filterDate.Date;
        }
        
        // Fall back to string comparison
        return cellStr.Equals(filterValue, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Cell Value Parser

    private static class CellValueParser
    {
        public static bool TryParseNumeric(object cellValue, out double result)
        {
            result = 0;
            
            if (cellValue == null) return false;
            
            // Handle different types of cell values
            switch (cellValue)
            {
                case double d:
                    result = d;
                    return true;
                case int i:
                    result = i;
                    return true;
                case decimal dec:
                    result = (double)dec;
                    return true;
                case string s:
                    // Clean string before parsing
                    s = s.Trim().Replace("$", "").Replace(",", "");
                    return double.TryParse(s, out result);
                default:
                    return double.TryParse(cellValue.ToString(), out result);
            }
        }

        public static DateTime? TryParseDate(object cellValue)
        {
            if (cellValue == null) return null;
            
            if (cellValue is DateTime dt) return dt;
            
            if (cellValue is double oaDate)
            {
                try { return DateTime.FromOADate(oaDate); }
                catch { return null; }
            }
            
            if (DateTime.TryParse(cellValue.ToString(), out var parsed))
                return parsed;
                
            return null;
        }
    }

    #endregion

    #region Data Models

    private class QueryResult
    {
        public bool Success { get; set; }
        public string Query { get; set; } = "";
        public object? Value { get; set; }
        public string? Formula { get; set; }
        public string? Explanation { get; set; }
        public string? Error { get; set; }
        public double Confidence { get; set; }
    }

    private class FormulaExecutionResult
    {
        public bool Success { get; set; }
        public object? Value { get; set; }
        public string? FormattedValue { get; set; }
        public string? Error { get; set; }
    }

    #endregion
}