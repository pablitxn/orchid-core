using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Aspose.Cells;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;

namespace Infrastructure.Ai.SemanticKernel.Services;

/// <summary>
/// Service for executing formulas and calculations on spreadsheet data
/// </summary>
public sealed class FormulaExecutionService : IFormulaExecutionService
{
    private readonly ILogger<FormulaExecutionService> _logger;
    private readonly IChatCompletionService _chatCompletion;
    private readonly IActivityPublisher _activityPublisher;
    private readonly ISpreadsheetAnalysisService _analysisService;
    private readonly ISandboxManagementService _sandboxService;

    private const int MaxRetries = 5;
    private const double BaseConfidence = 1.0;
    private const double ConfidenceDecrement = 0.15;

    public FormulaExecutionService(
        ILogger<FormulaExecutionService> logger,
        IChatCompletionService chatCompletion,
        IActivityPublisher activityPublisher,
        ISpreadsheetAnalysisService analysisService,
        ISandboxManagementService sandboxService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _chatCompletion = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
        _activityPublisher = activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));
        _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
        _sandboxService = sandboxService ?? throw new ArgumentNullException(nameof(sandboxService));
    }

    /// <inheritdoc/>
    public async Task<QueryExecutionResult> ExecuteQueryAsync(
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        QueryAnalysisResult analysis,
        CancellationToken cancellationToken = default)
    {
        var strategies = GetExecutionStrategies();
        QueryExecutionResult? bestResult = null;
        var errors = new List<string>();

        foreach (var (strategyName, strategyType) in strategies)
        {
            try
            {
                await _activityPublisher.PublishAsync("formula_execution.strategy_attempt", new
                {
                    strategy = strategyName,
                    query,
                    sandboxName = context.SandboxName
                });

                var result = await ExecuteStrategyAsync(
                    strategyType, workbook, sandbox, context, query, analysis, cancellationToken);

                if (result.Success)
                {
                    // Validate the result
                    var validated = await ValidateResultAsync(result, context, analysis, query, cancellationToken);

                    if (validated.Success && (bestResult == null || validated.Confidence > bestResult.Confidence))
                    {
                        bestResult = validated;

                        // High confidence result - stop trying
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
                _logger.LogWarning(ex, "Strategy {Strategy} failed", strategyName);
            }
        }

        return bestResult ?? new QueryExecutionResult
        {
            Query = query,
            Success = false,
            Error = $"All strategies failed: {string.Join("; ", errors)}",
            ExecutionStrategy = "None"
        };
    }

    /// <inheritdoc/>
    public async Task<FormulaStrategy> DetermineFormulaStrategyAsync(
        Worksheet sandbox,
        SandboxContext context,
        string query,
        QueryAnalysisResult analysis,
        CancellationToken cancellationToken = default)
    {
        var headers = _analysisService.ExtractHeaders(sandbox);
        var dataRows = _analysisService.CountDataRows(sandbox);

        var prompt = $"""
                      Generate an Excel formula strategy for this query.

                      Query: {query}

                      Sandbox sheet structure:
                      - Headers at row 1: {string.Join(", ", headers.Select(h => h.Name))}
                      - Data rows: 2 to {dataRows + 1}
                      - Total rows in sandbox: {dataRows}
                      - Original dataset size: {context.OriginalRowCount} rows
                      - Filters applied: {JsonSerializer.Serialize(context.AppliedFilters)}
                      - Full dataset preserved: {context.FullDatasetPreserved}

                      Required operation: {analysis.AggregationType}
                      {(analysis.GroupBy != null ? $"Group by: {analysis.GroupBy}" : "")}

                      CRITICAL INSTRUCTIONS:
                      1. Use absolute references for ranges (e.g., $A$2:$A$100)
                      2. For the sandbox data range, use rows 2 to {dataRows + 1}
                      3. Handle edge cases (division by zero, empty ranges)
                      4. If multiple steps are needed, describe helper columns

                      For percentage calculations:
                      - If calculating percentage of rows matching a condition:
                        * If sandbox contains ALL data: Use COUNTIF for condition / COUNTA for total
                        * The filters are CONDITIONS to check, not pre-applied filters
                      - Always multiply by 100 for percentage results

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

        await _activityPublisher.PublishAsync("formula_execution.determine_strategy", new
        {
            model = settings.ModelId,
            headers,
            dataRows,
            context = new
            {
                context.OriginalRowCount,
                context.FilteredRowCount,
                context.FullDatasetPreserved
            }
        });

        var response =
            await _chatCompletion.GetChatMessageContentsAsync(chatHistory, settings,
                cancellationToken: cancellationToken);
        var strategyResponse = JsonSerializer.Deserialize<FormulaStrategyResponse>(response[0].Content ?? "{}")
                               ?? new FormulaStrategyResponse();

        var strategy = new FormulaStrategy
        {
            Approach = strategyResponse.Approach,
            Formula = strategyResponse.Formula,
            Explanation = strategyResponse.Explanation,
            HelperColumns = strategyResponse.HelperColumns.Select(h => new HelperColumn
            {
                Name = h.Name,
                Formula = h.Formula,
                Purpose = h.Purpose
            }).ToList(),
            Confidence = 0.8
        };

        return strategy;
    }

    /// <inheritdoc/>
    public async Task<FormulaExecutionResult> ExecuteFormulaAsync(
        Workbook workbook,
        Worksheet worksheet,
        string formula,
        string targetCell = "Z1",
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var result = new FormulaExecutionResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get target cell
            var cell = worksheet.Cells[targetCell];
            cell.Formula = formula;

            // Calculate
            workbook.CalculateFormula();

            // Get result
            var value = cell.Value;
            var stringValue = cell.StringValue;

            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            await _activityPublisher.PublishAsync("formula_execution.formula_executed", new
            {
                formula,
                targetCell,
                value,
                stringValue,
                executionTimeMs = result.ExecutionTimeMs,
                success = !stringValue.StartsWith("#")
            });

            if (stringValue.StartsWith("#"))
            {
                result.Success = false;
                result.Error = $"Excel error: {stringValue}";
                result.ErrorType = stringValue;
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
            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.Success = false;
            result.Error = ex.Message;
            result.ErrorType = "Exception";

            _logger.LogError(ex, "Formula execution failed");
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<QueryExecutionResult> CalculateManuallyAsync(
        Worksheet worksheet,
        string query,
        QueryAnalysisResult analysis,
        SandboxContext context,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var result = new QueryExecutionResult
        {
            Query = query,
            ExecutionStrategy = ExecutionStrategyType.ManualCalculation.ToString()
        };

        try
        {
            var headers = _analysisService.ExtractHeaders(worksheet);

            // Handle percentage queries with filters
            if (IsPercentageQuery(query) && analysis.Filters.Any())
            {
                var (matchingCount, totalCount) = await CountMatchingRowsAsync(
                    worksheet, headers, analysis.Filters, cancellationToken);

                result.Success = true;
                result.Value = totalCount > 0 ? (matchingCount * 100.0 / totalCount) : 0;
                result.Explanation =
                    $"Manual calculation: {matchingCount} of {totalCount} rows match the criteria ({result.Value:F2}%)";
                result.Confidence = 0.95;
                result.Metadata["matchingRows"] = matchingCount;
                result.Metadata["totalRows"] = totalCount;
            }
            else
            {
                // Other types of calculations
                var columnData = await ExtractColumnDataAsync(
                    worksheet, headers, analysis.ColumnsNeeded, cancellationToken);

                if (!columnData.Any())
                {
                    result.Error = "No data found for specified columns";
                    return result;
                }

                result.Success = true;
                result.Value = CalculateAggregation(columnData, analysis.AggregationType);
                result.Explanation = $"Manual {analysis.AggregationType} calculation on {columnData.Count} values";
                result.Confidence = 0.85;
                result.Metadata["valueCount"] = columnData.Count;
            }

            await _activityPublisher.PublishAsync("formula_execution.manual_calculation", new
            {
                aggregationType = analysis.AggregationType,
                result = result.Value,
                confidence = result.Confidence,
                metadata = result.Metadata
            });
        }
        catch (Exception ex)
        {
            result.Error = $"Manual calculation failed: {ex.Message}";
            _logger.LogError(ex, "Manual calculation failed");
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<QueryExecutionResult> ValidateResultAsync(
        QueryExecutionResult result,
        SandboxContext context,
        QueryAnalysisResult analysis,
        string query,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        // Validate percentage results
        if (result.Success && IsPercentageQuery(query))
        {
            var percentage = Convert.ToDouble(result.Value);

            // Check for suspicious 100% results
            if (Math.Abs(percentage - 100) < 0.001 && context.AppliedFilters.Any())
            {
                await _activityPublisher.PublishAsync("formula_execution.suspicious_result", new
                {
                    result = percentage,
                    reason = "100% result with filters applied",
                    originalRows = context.OriginalRowCount,
                    filteredRows = context.FilteredRowCount,
                    fullDatasetPreserved = context.FullDatasetPreserved
                });

                if (!context.FullDatasetPreserved)
                {
                    result.Success = false;
                    result.Error = "Incorrect percentage calculation due to pre-filtered data";
                    result.Confidence = 0;
                }
                else
                {
                    result.Confidence = Math.Min(result.Confidence, 0.5);
                }
            }

            // Validate percentage range
            if (percentage < 0 || percentage > 100)
            {
                result.Success = false;
                result.Error = $"Invalid percentage value: {percentage}";
                result.Confidence = 0;
            }
        }

        // Validate numeric results
        if (result.Success && result.Value is double numValue)
        {
            if (double.IsNaN(numValue) || double.IsInfinity(numValue))
            {
                result.Success = false;
                result.Error = "Invalid numeric result (NaN or Infinity)";
                result.Confidence = 0;
            }
        }

        return result;
    }

    #region Private Methods

    private List<(string Name, ExecutionStrategyType Type)> GetExecutionStrategies()
    {
        return new List<(string, ExecutionStrategyType)>
        {
            ("Standard Formula", ExecutionStrategyType.StandardFormula),
            ("Helper Columns", ExecutionStrategyType.HelperColumns),
            ("Manual Calculation", ExecutionStrategyType.ManualCalculation),
            ("Sub-queries", ExecutionStrategyType.SubQueries)
        };
    }

    private async Task<QueryExecutionResult> ExecuteStrategyAsync(
        ExecutionStrategyType strategyType,
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        QueryAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        return strategyType switch
        {
            ExecutionStrategyType.StandardFormula =>
                await ExecuteStandardFormulaAsync(workbook, sandbox, context, query, analysis, cancellationToken),

            ExecutionStrategyType.HelperColumns =>
                await ExecuteWithHelperColumnsAsync(workbook, sandbox, context, query, analysis, cancellationToken),

            ExecutionStrategyType.ManualCalculation =>
                await CalculateManuallyAsync(sandbox, query, analysis, context, cancellationToken),

            ExecutionStrategyType.SubQueries =>
                await ExecuteAsSubQueriesAsync(workbook, sandbox, context, query, analysis, cancellationToken),

            _ => throw new NotSupportedException($"Strategy {strategyType} not supported")
        };
    }

    private async Task<QueryExecutionResult> ExecuteStandardFormulaAsync(
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        QueryAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        var result = new QueryExecutionResult
        {
            Query = query,
            ExecutionStrategy = ExecutionStrategyType.StandardFormula.ToString()
        };

        try
        {
            var strategy = await DetermineFormulaStrategyAsync(sandbox, context, query, analysis, cancellationToken);

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                var formulaResult =
                    await ExecuteFormulaAsync(workbook, sandbox, strategy.Formula, "Z1", cancellationToken);

                if (formulaResult.Success)
                {
                    result.Success = true;
                    result.Value = formulaResult.Value;
                    result.Formula = strategy.Formula;
                    result.Explanation = strategy.Explanation;
                    result.Confidence = CalculateConfidence(attempt, analysis);
                    break;
                }

                if (attempt < MaxRetries)
                {
                    // Refine strategy for next attempt
                    strategy = await RefineStrategyAsync(strategy, formulaResult.Error, sandbox, analysis,
                        cancellationToken);
                }
                else
                {
                    result.Error = formulaResult.Error;
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

    private async Task<QueryExecutionResult> ExecuteWithHelperColumnsAsync(
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        QueryAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        var result = new QueryExecutionResult
        {
            Query = query,
            ExecutionStrategy = ExecutionStrategyType.HelperColumns.ToString()
        };

        try
        {
            // Get strategy with helper columns
            var strategy = await DetermineFormulaStrategyAsync(sandbox, context, query, analysis, cancellationToken);

            // Apply helper columns
            await _sandboxService.ApplyHelperColumnsAsync(sandbox, strategy, cancellationToken);

            // Execute main formula
            var formulaResult = await ExecuteFormulaAsync(workbook, sandbox, strategy.Formula, "Z1", cancellationToken);

            if (formulaResult.Success)
            {
                result.Success = true;
                result.Value = formulaResult.Value;
                result.Formula = strategy.Formula;
                result.Explanation = $"{strategy.Explanation} (with {strategy.HelperColumns.Count} helper columns)";
                result.Confidence = 0.85;
                result.Metadata["helperColumnCount"] = strategy.HelperColumns.Count;
            }
            else
            {
                result.Error = formulaResult.Error;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<QueryExecutionResult> ExecuteAsSubQueriesAsync(
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        QueryAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        // For complex queries, break down into simpler sub-queries
        // This is a fallback strategy
        _logger.LogInformation("Attempting sub-query execution strategy");

        // For now, fall back to manual calculation
        return await CalculateManuallyAsync(sandbox, query, analysis, context, cancellationToken);
    }

    private async Task<FormulaStrategy> RefineStrategyAsync(
        FormulaStrategy currentStrategy,
        string error,
        Worksheet sandbox,
        QueryAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        var headers = _analysisService.ExtractHeaders(sandbox);
        var dataRows = _analysisService.CountDataRows(sandbox);

        var prompt = $"""
                      The previous formula failed. Please provide an alternative approach.

                      Failed formula: {currentStrategy.Formula}
                      Error: {error}

                      Sheet structure:
                      - Headers: {string.Join(", ", headers.Select(h => h.Name))}
                      - Data rows: 2 to {dataRows + 1}

                      Common solutions:
                      - #N/A: Use IFERROR or check if lookup value exists
                      - #DIV/0: Add IFERROR or check denominator != 0
                      - #VALUE: Check data types, use VALUE() for text-to-number
                      - #REF: Verify cell references are within bounds

                      Provide a more robust formula.
                      """;

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = "o4-mini",
            Temperature = 0.2
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, prompt);

        var response =
            await _chatCompletion.GetChatMessageContentsAsync(chatHistory, settings,
                cancellationToken: cancellationToken);

        return new FormulaStrategy
        {
            Approach = "Refined approach",
            Formula = ExtractFormula(response[0].Content ?? ""),
            Explanation = $"Refined after error: {error}",
            Confidence = currentStrategy.Confidence * 0.8
        };
    }

    private async Task<(int matchingCount, int totalCount)> CountMatchingRowsAsync(
        Worksheet worksheet,
        List<HeaderInfo> headers,
        List<FilterCriteria> filters,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var dataRange = _analysisService.GetDataRange(worksheet);
        int totalCount = 0;
        int matchingCount = 0;

        for (int row = dataRange.FirstRow; row <= dataRange.LastRow; row++)
        {
            totalCount++;

            if (_analysisService.RowMatchesFilters(worksheet, row, headers, filters))
            {
                matchingCount++;
            }
        }

        return (matchingCount, totalCount);
    }

    private async Task<List<double>> ExtractColumnDataAsync(
        Worksheet worksheet,
        List<HeaderInfo> headers,
        List<string> columnsNeeded,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var values = new List<double>();
        var columnIndex = -1;

        // Find the first matching column
        foreach (var neededColumn in columnsNeeded)
        {
            columnIndex = headers.FindIndex(h =>
                h.Name.Equals(neededColumn, StringComparison.OrdinalIgnoreCase));

            if (columnIndex >= 0) break;
        }

        if (columnIndex < 0) return values;

        // Extract numeric values
        var dataRows = _analysisService.CountDataRows(worksheet);

        for (int row = 1; row <= dataRows; row++)
        {
            var cellValue = worksheet.Cells[row, columnIndex].Value;
            if (TryParseNumeric(cellValue, out var numValue))
            {
                values.Add(numValue);
            }
        }

        return values;
    }

    private object CalculateAggregation(List<double> values, string aggregationType)
    {
        if (!values.Any()) return 0;

        return aggregationType.ToLower() switch
        {
            "sum" => values.Sum(),
            "average" or "avg" => values.Average(),
            "max" => values.Max(),
            "min" => values.Min(),
            "count" => values.Count,
            "variance" or "var" => CalculateVariance(values),
            "stdev" or "stddev" => Math.Sqrt(CalculateVariance(values)),
            "median" => CalculateMedian(values),
            _ => values.Sum()
        };
    }

    private double CalculateVariance(List<double> values)
    {
        if (values.Count < 2) return 0;

        var mean = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return sumOfSquares / (values.Count - 1);
    }

    private double CalculateMedian(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int count = sorted.Count;

        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }

        return sorted[count / 2];
    }

    private double CalculateConfidence(int attempts, QueryAnalysisResult analysis)
    {
        var confidence = BaseConfidence - (attempts - 1) * ConfidenceDecrement;

        // Boost confidence for simpler queries
        if (!analysis.RequiresCalculation && analysis.GroupBy == null)
        {
            confidence += 0.1;
        }

        return Math.Max(0.3, Math.Min(1.0, confidence));
    }

    private string ExtractFormula(string text)
    {
        // Extract formula from AI response
        var formulaMatch = Regex.Match(text, @"=[\w\s\$\(\):,\.\+\-\*/""<>]+", RegexOptions.IgnoreCase);
        return formulaMatch.Success ? formulaMatch.Value : text.Trim();
    }

    private bool IsPercentageQuery(string query)
    {
        var keywords = new[] { "percentage", "percent", "%", "proportion" };
        return keywords.Any(k => query.Contains(k, StringComparison.OrdinalIgnoreCase));
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

    #region Private DTOs

    private sealed class FormulaStrategyResponse
    {
        public string Approach { get; set; } = "";
        public string Formula { get; set; } = "";
        public List<HelperColumnResponse> HelperColumns { get; set; } = new();
        public string Explanation { get; set; } = "";
    }

    private sealed class HelperColumnResponse
    {
        public string Name { get; set; } = "";
        public string Formula { get; set; } = "";
        public string Purpose { get; set; } = "";
    }

    #endregion
}