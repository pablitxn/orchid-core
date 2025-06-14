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
/// Enhanced Spreadsheet Plugin with intelligent planning, verification, and navigation capabilities
/// for accurate Excel formula generation and data analysis.
/// </summary>
public sealed class SpreadsheetPluginV2(
    ILogger<SpreadsheetPluginV2> logger,
    IMemoryCache memoryCache,
    IDistributedCache distributedCache,
    IFileStorageService fileStorage,
    IActivityPublisher activityPublisher,
    IChatCompletionService chatCompletion,
    int initialAnalysisRows = 60,
    int sampleRows = 20)
{
    private readonly ILogger<SpreadsheetPluginV2> _log = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IMemoryCache _mem = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

    private readonly IDistributedCache _redis =
        distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));

    private readonly IFileStorageService _storage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));

    private readonly IActivityPublisher _activity =
        activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));

    private readonly IChatCompletionService _chat =
        chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));

    private readonly int _initialAnalysisRows = Math.Max(10, initialAnalysisRows);
    private readonly int _sampleRows = Math.Max(5, sampleRows);

    #region Structured Output Models

    private sealed class NavigationPlanResponse
    {
        [JsonPropertyName("steps")] public List<NavigationStep> Steps { get; set; } = new();
        [JsonPropertyName("confidence")] public double Confidence { get; set; }
        [JsonPropertyName("reasoning")] public string Reasoning { get; set; } = "";
    }

    private sealed class NavigationStep
    {
        [JsonPropertyName("action")] public string Action { get; set; } = "";
        [JsonPropertyName("target")] public string Target { get; set; } = "";
        [JsonPropertyName("purpose")] public string Purpose { get; set; } = "";
        [JsonPropertyName("fallback")] public string? Fallback { get; set; }
    }

    private sealed class FormulaGenerationResponse
    {
        [JsonPropertyName("formula")] public string Formula { get; set; } = "";
        [JsonPropertyName("explanation")] public string Explanation { get; set; } = "";
        [JsonPropertyName("assumptions")] public List<string> Assumptions { get; set; } = new();
    }

    private sealed class VerificationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public Dictionary<string, object> Evidence { get; set; } = new();
    }

    #endregion

    #region Main Entry Points

    [KernelFunction("formula_query")]
    [Description(
        "Generates an Excel formula based on natural language description with intelligent planning and verification")]
    public async Task<string> FormulaQueryAsync(
        [Description("Full path of the workbook on disk")]
        string filePath,
        [Description("Natural language description of desired formula")]
        string goal,
        [Description("Worksheet name; defaults to the first sheet")]
        string sheetName = "")
    {
        _log.LogInformation("FormulaQuery → {File}/{Sheet}: '{Goal}'", filePath, sheetName, goal);

        try
        {
            // Phase 1: Initial Analysis
            var analysis = await GetOrComputeAnalysisAsync(filePath, sheetName);

            // Phase 2: Create Navigation Plan
            var plan = await CreateNavigationPlanAsync(goal, analysis);
            _log.LogInformation("Created navigation plan with {Count} steps", plan.Steps.Count);

            // Phase 3: Execute Plan with Verification
            var context = await ExecuteNavigationPlanAsync(filePath, sheetName, plan, analysis);

            // Phase 4: Generate Formula with Verified Context
            var result = await GenerateFormulaWithVerifiedContextAsync(filePath, sheetName, goal, context, analysis);

            await _activity.PublishAsync("formula_query", new
            {
                filePath,
                sheetName,
                goal,
                plan,
                result
            });

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "FormulaQuery failed");
            throw;
        }
    }

    [KernelFunction("query_data")]
    [Description("Queries and analyzes Excel data based on natural language questions")]
    public async Task<string> QueryDataAsync(
        [Description("Full path of the workbook on disk")]
        string filePath,
        [Description("Natural language question about the data")]
        string question,
        [Description("Worksheet name; defaults to the first sheet")]
        string sheetName = "")
    {
        _log.LogInformation("QueryData → {File}/{Sheet}: '{Question}'", filePath, sheetName, question);

        try
        {
            var analysis = await GetOrComputeAnalysisAsync(filePath, sheetName);
            var plan = await CreateNavigationPlanAsync(question, analysis);
            var context = await ExecuteNavigationPlanAsync(filePath, sheetName, plan, analysis);

            // For data queries, we also compute the actual values
            var result = await AnalyzeDataWithContextAsync(filePath, sheetName, question, context, analysis);

            await _activity.PublishAsync("query_data", new
            {
                filePath,
                sheetName,
                question,
                result
            });

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "QueryData failed");
            throw;
        }
    }

    #endregion

    #region Planning Phase

    private async Task<NavigationPlanResponse> CreateNavigationPlanAsync(string goal, ExcelAnalysis analysis)
    {
        var prompt = $"""
                      Create a step-by-step navigation plan to fulfill this Excel query.

                      Goal: {goal}

                      Available Structure:
                      - Headers: {string.Join(", ", analysis.ColumnMetadata.Keys)}
                      - Data rows: {analysis.Structure.FirstDataRow + 1} to {analysis.Structure.LastDataRow + 1}
                      - Column types: {JsonSerializer.Serialize(analysis.ColumnMetadata.ToDictionary(k => k.Key, v => v.Value.DominantType.ToString()))}

                      Create a plan with specific steps like:
                      - find_column: Locate a specific column by name
                      - verify_values: Check if specific values exist in a column
                      - get_unique_values: Get all unique values from a column
                      - count_matches: Count rows matching criteria
                      - sample_data: Get sample rows matching criteria

                      Each step should have:
                      - action: The operation to perform
                      - target: What to look for
                      - purpose: Why this step is needed
                      - fallback: What to do if this fails

                      Be specific about what needs to be verified before generating a formula.
                      """;

        try
        {
            var planResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "navigation_plan",
                jsonSchema: BinaryData.FromString("""
                                                  {
                                                    "type": "object",
                                                    "properties": {
                                                      "steps": {
                                                        "type": "array",
                                                        "items": {
                                                          "type": "object",
                                                          "properties": {
                                                            "action":   { "type": "string" },
                                                            "target":   { "type": "string" },
                                                            "purpose":  { "type": "string" },
                                                            "fallback": { "type": ["string", "null"] }
                                                          },
                                                          "required": [ "action", "target", "purpose", "fallback" ],
                                                          "additionalProperties": false
                                                        }
                                                      },
                                                      "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
                                                      "reasoning":  { "type": "string" }
                                                    },
                                                    "required": [ "steps", "confidence", "reasoning" ],
                                                    "additionalProperties": false
                                                  }
                                                  """),
                jsonSchemaIsStrict: true
            );

            var settings = new OpenAIPromptExecutionSettings
            {
                ModelId = "o4-mini",
                ResponseFormat = planResponseFormat
            };

            var chatHistory = new ChatHistory();
            chatHistory.AddMessage(AuthorRole.User, prompt);

            var response = await _chat.GetChatMessageContentsAsync(chatHistory, settings);
            var plan = JsonSerializer.Deserialize<NavigationPlanResponse>(response[0].Content ?? "{}");

            return plan ?? new NavigationPlanResponse { Confidence = 0.5 };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to create navigation plan");
            // Fallback to basic plan
            return new NavigationPlanResponse
            {
                Steps = new List<NavigationStep>
                {
                    new() { Action = "analyze_structure", Target = "all", Purpose = "Understand data layout" },
                    new()
                    {
                        Action = "generate_formula", Target = "goal", Purpose = "Create formula based on structure"
                    }
                },
                Confidence = 0.3,
                Reasoning = "Fallback plan due to planning error"
            };
        }
    }

    #endregion

    #region Navigation Execution

    private async Task<EnhancedContext> ExecuteNavigationPlanAsync(
        string filePath,
        string sheetName,
        NavigationPlanResponse plan,
        ExcelAnalysis analysis)
    {
        var context = new EnhancedContext
        {
            NavigationPlan = plan,
            BaseAnalysis = analysis
        };

        using var wb = await LoadWorkbookAsync(filePath);
        var ws = GetWorksheet(wb, sheetName);

        foreach (var step in plan.Steps)
        {
            _log.LogDebug("Executing navigation step: {Action} on {Target}", step.Action, step.Target);

            try
            {
                var result = step.Action.ToLower() switch
                {
                    "find_column" => await FindColumnAsync(ws, step.Target, analysis),
                    "verify_values" => await VerifyValuesExistAsync(ws, step.Target, analysis),
                    "get_unique_values" => await GetUniqueValuesAsync(ws, step.Target, analysis),
                    "count_matches" => await CountMatchesAsync(ws, step.Target, analysis),
                    "sample_data" => await SampleMatchingDataAsync(ws, step.Target, analysis),
                    _ => new VerificationResult { Success = false, Message = $"Unknown action: {step.Action}" }
                };

                context.VerificationResults[step.Target] = result;

                if (!result.Success && !string.IsNullOrEmpty(step.Fallback))
                {
                    _log.LogWarning("Step failed, executing fallback: {Fallback}", step.Fallback);
                    // Execute fallback logic here if needed
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to execute step: {Action}", step.Action);
                context.VerificationResults[step.Target] = new VerificationResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        // Build enhanced sample based on verification results
        context.EnhancedSample = await BuildEnhancedSampleAsync(ws, context, analysis);

        return context;
    }

    private async Task<VerificationResult> FindColumnAsync(Worksheet ws, string target, ExcelAnalysis analysis)
    {
        await Task.CompletedTask;

        var result = new VerificationResult();
        var targetLower = target.ToLower();

        var matches = analysis.ColumnMetadata
            .Where(c => c.Key.ToLower().Contains(targetLower))
            .ToList();

        if (matches.Any())
        {
            result.Success = true;
            result.Message = $"Found {matches.Count} matching columns";
            result.Evidence["columns"] =
                matches.Select(m => new { m.Key, m.Value.Letter, m.Value.DominantType }).ToList();
        }
        else
        {
            result.Success = false;
            result.Message = $"No columns found matching '{target}'";
        }

        return result;
    }

    private async Task<VerificationResult> VerifyValuesExistAsync(Worksheet ws, string target, ExcelAnalysis analysis)
    {
        await Task.CompletedTask;

        var result = new VerificationResult();

        // Parse target like "PaymentType=DIV"
        var parts = target.Split('=');
        if (parts.Length != 2)
        {
            result.Success = false;
            result.Message = "Invalid target format. Expected: ColumnName=Value";
            return result;
        }

        var columnName = parts[0].Trim();
        var searchValue = parts[1].Trim().Trim('"', '\'');

        // Find column
        var column = analysis.ColumnMetadata.FirstOrDefault(c =>
            c.Key.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        if (column.Value == null)
        {
            result.Success = false;
            result.Message = $"Column '{columnName}' not found";
            return result;
        }

        // Search for value
        int count = 0;
        var matchingRows = new List<int>();

        for (int r = analysis.Structure.FirstDataRow; r <= analysis.Structure.LastDataRow; r++)
        {
            var cellValue = ws.Cells[r, column.Value.Index].StringValue.Trim();
            if (cellValue.Equals(searchValue, StringComparison.OrdinalIgnoreCase))
            {
                count++;
                if (matchingRows.Count < 10) // Keep first 10 for evidence
                    matchingRows.Add(r + 1); // Convert to 1-based for user
            }
        }

        result.Success = count > 0;
        result.Message = count > 0
            ? $"Found '{searchValue}' in {count} rows"
            : $"Value '{searchValue}' not found in column '{columnName}'";
        result.Evidence["count"] = count;
        result.Evidence["sample_rows"] = matchingRows;
        result.Evidence["column_letter"] = column.Value.Letter;

        return result;
    }

    private async Task<VerificationResult> GetUniqueValuesAsync(Worksheet ws, string columnName, ExcelAnalysis analysis)
    {
        await Task.CompletedTask;

        var result = new VerificationResult();

        var column = analysis.ColumnMetadata.FirstOrDefault(c =>
            c.Key.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        if (column.Value == null)
        {
            result.Success = false;
            result.Message = $"Column '{columnName}' not found";
            return result;
        }

        var uniqueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int r = analysis.Structure.FirstDataRow; r <= analysis.Structure.LastDataRow; r++)
        {
            var value = ws.Cells[r, column.Value.Index].StringValue.Trim();
            if (!string.IsNullOrEmpty(value))
                uniqueValues.Add(value);
        }

        result.Success = true;
        result.Message = $"Found {uniqueValues.Count} unique values";
        result.Evidence["unique_values"] = uniqueValues.OrderBy(v => v).ToList();
        result.Evidence["column_letter"] = column.Value.Letter;

        return result;
    }

    private async Task<VerificationResult> CountMatchesAsync(Worksheet ws, string criteria, ExcelAnalysis analysis)
    {
        await Task.CompletedTask;

        var result = new VerificationResult();

        // Simple implementation - could be extended for complex criteria
        var parts = criteria.Split('=');
        if (parts.Length == 2)
        {
            return await VerifyValuesExistAsync(ws, criteria, analysis);
        }

        result.Success = false;
        result.Message = "Invalid criteria format";
        return result;
    }

    private async Task<VerificationResult> SampleMatchingDataAsync(Worksheet ws, string criteria,
        ExcelAnalysis analysis)
    {
        await Task.CompletedTask;

        var result = new VerificationResult();

        // Parse criteria
        var parts = criteria.Split('=');
        if (parts.Length != 2)
        {
            result.Success = false;
            result.Message = "Invalid criteria format";
            return result;
        }

        var columnName = parts[0].Trim();
        var searchValue = parts[1].Trim().Trim('"', '\'');

        var column = analysis.ColumnMetadata.FirstOrDefault(c =>
            c.Key.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        if (column.Value == null)
        {
            result.Success = false;
            result.Message = $"Column '{columnName}' not found";
            return result;
        }

        var samples = new List<Dictionary<string, string>>();

        for (int r = analysis.Structure.FirstDataRow; r <= analysis.Structure.LastDataRow && samples.Count < 5; r++)
        {
            var cellValue = ws.Cells[r, column.Value.Index].StringValue.Trim();
            if (cellValue.Equals(searchValue, StringComparison.OrdinalIgnoreCase))
            {
                var row = new Dictionary<string, string>();
                foreach (var col in analysis.ColumnMetadata)
                {
                    row[col.Key] = ws.Cells[r, col.Value.Index].StringValue.Trim();
                }

                samples.Add(row);
            }
        }

        result.Success = samples.Any();
        result.Message = samples.Any()
            ? $"Found {samples.Count} sample rows"
            : $"No rows found matching criteria";
        result.Evidence["samples"] = samples;

        return result;
    }

    private async Task<string> BuildEnhancedSampleAsync(Worksheet ws, EnhancedContext context, ExcelAnalysis analysis)
    {
        await Task.CompletedTask;

        var sb = new StringBuilder();

        // Add verification summary
        sb.AppendLine("## Verification Summary");
        foreach (var (key, result) in context.VerificationResults)
        {
            sb.AppendLine($"- {key}: {(result.Success ? "✓" : "✗")} {result.Message}");
        }

        sb.AppendLine();

        // Add evidence details
        sb.AppendLine("## Key Findings");
        foreach (var (key, result) in context.VerificationResults.Where(r => r.Value.Success))
        {
            if (result.Evidence.TryGetValue("unique_values", out var value))
            {
                var values = (List<string>)value;
                sb.AppendLine($"- {key} unique values: {string.Join(", ", values.Take(10))}");
            }

            if (result.Evidence.TryGetValue("count", out var value1))
            {
                sb.AppendLine($"- {key} matches: {value1}");
            }
        }

        sb.AppendLine();

        // Add original sample
        sb.AppendLine("## Data Sample");
        sb.AppendLine(analysis.SampleData.MarkdownTable);

        return sb.ToString();
    }

    #endregion

    #region Formula Generation with Verification

    private async Task<FormulaResult> GenerateFormulaWithVerifiedContextAsync(
        string filePath,
        string sheetName,
        string goal,
        EnhancedContext context,
        ExcelAnalysis analysis)
    {
        const int maxAttempts = 10;
        var attempts = new List<FormulaAttempt>();

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var formulaAttempt = new FormulaAttempt { AttemptNumber = attempt };

            try
            {
                // Build prompt with verification results
                var prompt = BuildEnhancedFormulaPrompt(goal, context, analysis, attempts);

                // Generate formula
                var formulaResponse = await GenerateFormulaAsync(prompt);
                formulaAttempt.Formula = formulaResponse.Formula;
                formulaAttempt.Explanation = formulaResponse.Explanation;

                // Validate formula
                using var wb = await LoadWorkbookAsync(filePath);
                var ws = GetWorksheet(wb, sheetName);

                var validation = await ValidateFormulaAsync(wb, ws, formulaResponse.Formula);
                formulaAttempt.ValidationResult = validation;

                if (validation.Success)
                {
                    // Success!
                    return new FormulaResult
                    {
                        Success = true,
                        Formula = formulaResponse.Formula,
                        Value = validation.Value,
                        Explanation = formulaResponse.Explanation,
                        Attempts = attempts.Count + 1,
                        VerificationContext = context.VerificationResults,
                        Confidence = CalculateConfidence(attempt, context)
                    };
                }

                // Formula failed - investigate why
                formulaAttempt.ErrorAnalysis = await InvestigateFormulaErrorAsync(
                    ws, formulaResponse.Formula, validation.Error, context, analysis);
            }
            catch (Exception ex)
            {
                formulaAttempt.Exception = ex.Message;
                _log.LogWarning(ex, "Attempt {Attempt} failed", attempt);
            }

            attempts.Add(formulaAttempt);
        }

        // All attempts failed
        return new FormulaResult
        {
            Success = false,
            Formula = attempts.LastOrDefault()?.Formula ?? "",
            Error = "Failed to generate working formula after all attempts",
            Attempts = attempts.Count,
            AttemptHistory = attempts
        };
    }

    private async Task<FormulaGenerationResponse> GenerateFormulaAsync(string prompt)
    {
        var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "formula_generation",
            jsonSchema: BinaryData.FromString("""
                                              {
                                                "type": "object",
                                                "properties": {
                                                  "formula":     { "type": "string" },
                                                  "explanation": { "type": "string" },
                                                  "assumptions": {
                                                    "type":  "array",
                                                    "items": { "type": "string" }
                                                  }
                                                },
                                                "required": [ "formula", "explanation", "assumptions" ],
                                                "additionalProperties": false        
                                              }
                                              """),
            jsonSchemaIsStrict: true
        );

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = "o4-mini",
            ResponseFormat = responseFormat
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, prompt);

        var response = await _chat.GetChatMessageContentsAsync(chatHistory, settings);
        return JsonSerializer.Deserialize<FormulaGenerationResponse>(response[0].Content ?? "{}")
               ?? new FormulaGenerationResponse();
    }

    private async Task<FormulaValidation> ValidateFormulaAsync(Workbook wb, Worksheet ws, string formula)
    {
        await Task.CompletedTask;

        try
        {
            var tempName = $"Temp_{Guid.NewGuid():N}"[..15];
            var tempSheet = wb.Worksheets.Add(tempName);

            tempSheet.Cells["A1"].Formula = formula;
            wb.CalculateFormula();

            var cell = tempSheet.Cells["A1"];
            var cellText = cell.StringValue;
            var value = cell.Value;

            wb.Worksheets.RemoveAt(wb.Worksheets.IndexOf(tempSheet));

            if (cellText.StartsWith("#"))
            {
                return new FormulaValidation
                {
                    Success = false,
                    Error = cellText,
                    ErrorType = "Excel Error"
                };
            }

            return new FormulaValidation
            {
                Success = true,
                Value = value,
                FormattedValue = cellText
            };
        }
        catch (Exception ex)
        {
            return new FormulaValidation
            {
                Success = false,
                Error = ex.Message,
                ErrorType = "Validation Exception"
            };
        }
    }

    private async Task<string> InvestigateFormulaErrorAsync(
        Worksheet ws,
        string formula,
        string error,
        EnhancedContext context,
        ExcelAnalysis analysis)
    {
        await Task.CompletedTask;

        var investigation = new StringBuilder();
        investigation.AppendLine($"Error: {error}");

        // Check if error is due to missing values
        if (error.Contains("#N/A") || error.Contains("DIV/0"))
        {
            investigation.AppendLine("Possible cause: Referenced values not found or division by zero");

            // Extract column references from formula
            var columnRefs = ExtractColumnReferences(formula);
            foreach (var colRef in columnRefs)
            {
                investigation.AppendLine($"- Column {colRef}: Check if values exist in expected range");
            }
        }

        // Check if ranges are correct
        var rangePattern = @"\$?([A-Z]+)\$?(\d+):\$?([A-Z]+)\$?(\d+)";
        var matches = Regex.Matches(formula, rangePattern);
        foreach (Match match in matches)
        {
            var startRow = int.Parse(match.Groups[2].Value);
            var endRow = int.Parse(match.Groups[4].Value);

            if (startRow > analysis.Structure.LastDataRow || endRow > analysis.Structure.LastDataRow)
            {
                investigation.AppendLine(
                    $"Range issue: Formula references row {Math.Max(startRow, endRow)} but data ends at row {analysis.Structure.LastDataRow + 1}");
            }
        }

        return investigation.ToString();
    }

    private string BuildEnhancedFormulaPrompt(
        string goal,
        EnhancedContext context,
        ExcelAnalysis analysis,
        List<FormulaAttempt> previousAttempts)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            "You are an Excel formula expert. Generate a precise Excel formula based on verified information.");
        sb.AppendLine();

        if (previousAttempts.Any())
        {
            sb.AppendLine("PREVIOUS ATTEMPTS:");
            foreach (var attempt in previousAttempts.TakeLast(3))
            {
                sb.AppendLine($"  Attempt {attempt.AttemptNumber}: {attempt.Formula}");
                if (!string.IsNullOrEmpty(attempt.ValidationResult?.Error))
                    sb.AppendLine($"  Error: {attempt.ValidationResult.Error}");
                if (!string.IsNullOrEmpty(attempt.ErrorAnalysis))
                    sb.AppendLine($"  Analysis: {attempt.ErrorAnalysis}");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"GOAL: {goal}");
        sb.AppendLine();

        sb.AppendLine("VERIFIED INFORMATION:");
        foreach (var (key, result) in context.VerificationResults.Where(r => r.Value.Success))
        {
            sb.AppendLine($"✓ {key}: {result.Message}");
            foreach (var (evidenceKey, evidenceValue) in result.Evidence)
            {
                if (evidenceKey == "column_letter")
                    sb.AppendLine($"  - Column: {evidenceValue}");
                else if (evidenceKey == "count")
                    sb.AppendLine($"  - Count: {evidenceValue}");
                else if (evidenceKey == "unique_values" && evidenceValue is List<string> values)
                    sb.AppendLine($"  - Values: {string.Join(", ", values.Take(5))}");
            }
        }

        sb.AppendLine();

        sb.AppendLine("WORKBOOK STRUCTURE:");
        sb.AppendLine($"  Headers at row: {analysis.Structure.HeaderRow + 1}");
        sb.AppendLine($"  Data rows: {analysis.Structure.FirstDataRow + 1} to {analysis.Structure.LastDataRow + 1}");
        sb.AppendLine($"  Total data rows: {analysis.Structure.TotalRows}");
        sb.AppendLine();

        sb.AppendLine("COLUMN MAPPING:");
        foreach (var (header, meta) in analysis.ColumnMetadata)
        {
            sb.AppendLine($"  {meta.Letter}: \"{header}\" (Type: {meta.DominantType})");
        }

        sb.AppendLine();

        sb.AppendLine("CRITICAL INSTRUCTIONS:");
        sb.AppendLine("- Use the EXACT ranges verified above");
        sb.AppendLine("- Use absolute references (e.g., $F$2:$F$23)");
        sb.AppendLine("- For the data range, use rows 2 to " + (analysis.Structure.LastDataRow + 1));
        sb.AppendLine("- If filtering by text values, use exact case from verified values");
        sb.AppendLine("- Return a working Excel formula, not a description");

        return sb.ToString();
    }

    private List<string> ExtractColumnReferences(string formula)
    {
        var pattern = @"\$?([A-Z]+)\$?\d+";
        var matches = Regex.Matches(formula, pattern);
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }

    private double CalculateConfidence(int attempts, EnhancedContext context)
    {
        var baseConfidence = 1.0 - (attempts - 1) * 0.1;
        var verificationBonus = context.VerificationResults.Count(r => r.Value.Success) * 0.05;
        return Math.Max(0.1, Math.Min(1.0, baseConfidence + verificationBonus));
    }

    #endregion

    #region Data Analysis

    private async Task<DataAnalysisResult> AnalyzeDataWithContextAsync(
        string filePath,
        string sheetName,
        string question,
        EnhancedContext context,
        ExcelAnalysis analysis)
    {
        // First try to generate a formula
        var formulaResult =
            await GenerateFormulaWithVerifiedContextAsync(filePath, sheetName, question, context, analysis);

        var result = new DataAnalysisResult
        {
            Question = question,
            Formula = formulaResult.Formula,
            ComputedValue = formulaResult.Value,
            Success = formulaResult.Success,
            Confidence = formulaResult.Confidence
        };

        // Add insights based on verification
        var insights = new List<string>();

        foreach (var (key, verification) in context.VerificationResults.Where(v => v.Value.Success))
        {
            if (verification.Evidence.ContainsKey("count"))
            {
                insights.Add($"Found {verification.Evidence["count"]} matching records for {key}");
            }

            if (verification.Evidence.ContainsKey("unique_values") &&
                verification.Evidence["unique_values"] is List<string> values)
            {
                insights.Add($"{key} contains {values.Count} unique values: {string.Join(", ", values.Take(3))}...");
            }
        }

        result.Insights = insights;

        // Build explanation
        if (formulaResult.Success)
        {
            result.Explanation = $"The answer is {formulaResult.Value}. " +
                                 $"This was calculated using the formula: {formulaResult.Formula}. " +
                                 formulaResult.Explanation;
        }
        else
        {
            result.Explanation = "Unable to calculate the exact value due to formula generation issues. " +
                                 $"However, based on the data analysis: {string.Join(" ", insights)}";
        }

        return result;
    }

    #endregion

    #region Core Analysis (from original)

    private async Task<ExcelAnalysis> GetOrComputeAnalysisAsync(string filePath, string sheetName)
    {
        var cacheKey = $"excel-analysis::{filePath}::{sheetName.ToLowerInvariant()}::{_initialAnalysisRows}";

        if (_mem.TryGetValue(cacheKey, out object? cached) && cached is ExcelAnalysis cachedAnalysis)
        {
            _log.LogDebug("Analysis cache hit for {Key}", cacheKey);
            return cachedAnalysis;
        }

        _log.LogInformation("Computing fresh analysis for {File}/{Sheet}", filePath, sheetName);

        using var wb = await LoadWorkbookAsync(filePath);
        var ws = GetWorksheet(wb, sheetName);

        var analysis = new ExcelAnalysis
        {
            FilePath = filePath,
            SheetName = ws.Name,
            Structure = await AnalyzeStructureAsync(ws),
            ColumnMetadata = await AnalyzeColumnsAsync(ws),
            DataPatterns = await DetectPatternsAsync(ws),
            SampleData = await ExtractStratifiedSampleAsync(ws)
        };

        // Cache locally and in Redis
        _mem.Set(cacheKey, analysis, TimeSpan.FromHours(6));
        try
        {
            var json = JsonSerializer.Serialize(analysis);
            await _redis.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(24)
            });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Redis unavailable for analysis cache");
        }

        return analysis;
    }

    private async Task<WorksheetStructure> AnalyzeStructureAsync(Worksheet ws)
    {
        await Task.CompletedTask;

        var dim = ws.Cells.MaxDisplayRange ?? throw new InvalidOperationException("Empty worksheet");

        // Find header row
        int headerRow = dim.FirstRow;
        for (int r = dim.FirstRow; r <= Math.Min(dim.FirstRow + 10, dim.RowCount); r++)
        {
            int nonEmptyCount = 0;
            for (int c = dim.FirstColumn; c <= dim.ColumnCount; c++)
            {
                if (!string.IsNullOrWhiteSpace(ws.Cells[r, c].StringValue))
                    nonEmptyCount++;
            }

            if (nonEmptyCount >= 3)
            {
                headerRow = r;
                break;
            }
        }

        // Detect structure type
        var structureType = DetectStructureType(ws, headerRow);

        // Find data boundaries
        int firstDataRow = headerRow + 1;
        int lastDataRow = dim.RowCount;

        // Detect empty columns
        var emptyColumns = new List<int>();
        for (int c = dim.FirstColumn; c <= dim.ColumnCount; c++)
        {
            bool hasData = false;
            for (int r = firstDataRow; r <= Math.Min(firstDataRow + 50, lastDataRow); r++)
            {
                if (!string.IsNullOrWhiteSpace(ws.Cells[r, c].StringValue))
                {
                    hasData = true;
                    break;
                }
            }

            if (!hasData)
                emptyColumns.Add(c);
        }

        return new WorksheetStructure
        {
            Type = structureType,
            HeaderRow = headerRow,
            FirstDataRow = firstDataRow,
            LastDataRow = lastDataRow,
            FirstColumn = dim.FirstColumn,
            LastColumn = dim.ColumnCount,
            EmptyColumns = emptyColumns,
            TotalRows = lastDataRow - firstDataRow + 1,
            TotalColumns = dim.ColumnCount - dim.FirstColumn + 1 - emptyColumns.Count
        };
    }

    private StructureType DetectStructureType(Worksheet ws, int headerRow)
    {
        bool hasMergedCells = false;

        for (int c = 0; c <= Math.Min(10, ws.Cells.MaxColumn); c++)
        {
            if (ws.Cells[headerRow, c].IsMerged)
                hasMergedCells = true;
        }

        var firstColValues = new HashSet<string>();
        for (int r = headerRow + 1; r <= Math.Min(headerRow + 20, ws.Cells.MaxRow); r++)
        {
            var val = ws.Cells[r, 0].StringValue.Trim();
            if (!string.IsNullOrEmpty(val))
                firstColValues.Add(val);
        }

        if (hasMergedCells || (firstColValues.Count > 10 &&
                               firstColValues.Count == Math.Min(20, ws.Cells.MaxRow - headerRow)))
            return StructureType.PivotTable;

        bool hasSubtotals = false;
        for (int r = headerRow + 1; r <= Math.Min(headerRow + 50, ws.Cells.MaxRow); r++)
        {
            var cellText = ws.Cells[r, 0].StringValue.ToLower();
            if (cellText.Contains("total") || cellText.Contains("subtotal"))
            {
                hasSubtotals = true;
                break;
            }
        }

        if (hasSubtotals)
            return StructureType.GroupedData;

        return StructureType.SimpleTable;
    }

    private async Task<Dictionary<string, ColumnMetadata>> AnalyzeColumnsAsync(Worksheet ws)
    {
        await Task.CompletedTask;

        var structure = await AnalyzeStructureAsync(ws);
        var metadata = new Dictionary<string, ColumnMetadata>();

        for (int c = structure.FirstColumn; c <= structure.LastColumn; c++)
        {
            if (structure.EmptyColumns.Contains(c))
                continue;

            var header = ws.Cells[structure.HeaderRow, c].StringValue.Trim();
            if (string.IsNullOrEmpty(header))
                header = $"Column{c}";

            var colMeta = new ColumnMetadata
            {
                Index = c,
                Header = header,
                Letter = GetColumnLetter(c)
            };

            var typeCount = new Dictionary<DataType, int>();
            int emptyCount = 0;
            var sampleValues = new List<string>();

            for (int r = structure.FirstDataRow; r <= Math.Min(structure.FirstDataRow + 50, structure.LastDataRow); r++)
            {
                var cell = ws.Cells[r, c];
                var value = cell.StringValue.Trim();

                if (string.IsNullOrEmpty(value))
                {
                    emptyCount++;
                    continue;
                }

                if (sampleValues.Count < 5)
                    sampleValues.Add(value);

                var dataType = InferCellType(cell, value);
                typeCount[dataType] = typeCount.GetValueOrDefault(dataType) + 1;
            }

            colMeta.DominantType =
                typeCount.Any() ? typeCount.OrderByDescending(x => x.Value).First().Key : DataType.Empty;
            colMeta.EmptyPercentage =
                (emptyCount * 100.0) / Math.Min(50, structure.LastDataRow - structure.FirstDataRow + 1);
            colMeta.SampleValues = sampleValues;
            colMeta.HasFormulas = DetectFormulas(ws, c, structure);

            if (colMeta.DominantType == DataType.Number)
            {
                var numbers = new List<double>();
                for (int r = structure.FirstDataRow;
                     r <= Math.Min(structure.FirstDataRow + 100, structure.LastDataRow);
                     r++)
                {
                    if (double.TryParse(ws.Cells[r, c].StringValue, out var num))
                        numbers.Add(num);
                }

                if (numbers.Any())
                {
                    colMeta.NumericRange = new NumericRange
                    {
                        Min = numbers.Min(),
                        Max = numbers.Max(),
                        Average = numbers.Average()
                    };
                }
            }

            metadata[header] = colMeta;
        }

        return metadata;
    }

    private async Task<DataPatterns> DetectPatternsAsync(Worksheet ws)
    {
        await Task.CompletedTask;

        var structure = await AnalyzeStructureAsync(ws);
        var patterns = new DataPatterns();

        for (int r = structure.FirstDataRow; r <= Math.Min(structure.FirstDataRow + 200, structure.LastDataRow); r++)
        {
            for (int c = structure.FirstColumn; c <= Math.Min(structure.FirstColumn + 5, structure.LastColumn); c++)
            {
                var value = ws.Cells[r, c].StringValue.ToLower();
                if (value.Contains("total") || value.Contains("subtotal") || value.Contains("sum"))
                {
                    patterns.SubtotalRows.Add(r);
                    break;
                }
            }
        }

        foreach (var col in Enumerable.Range(structure.FirstColumn, structure.LastColumn - structure.FirstColumn + 1))
        {
            int dateCount = 0;
            for (int r = structure.FirstDataRow; r <= Math.Min(structure.FirstDataRow + 20, structure.LastDataRow); r++)
            {
                if (ws.Cells[r, col].Type == CellValueType.IsDateTime)
                    dateCount++;
            }

            if (dateCount > 10)
                patterns.DateColumns.Add(col);
        }

        patterns.HasMergedCells = ws.Cells.MergedCells.Count > 0;

        patterns.IsConsistent = true;
        for (int c = structure.FirstColumn; c <= structure.LastColumn; c++)
        {
            var types = new HashSet<CellValueType>();
            for (int r = structure.FirstDataRow; r <= Math.Min(structure.FirstDataRow + 50, structure.LastDataRow); r++)
            {
                var cell = ws.Cells[r, c];
                if (!string.IsNullOrEmpty(cell.StringValue))
                    types.Add(cell.Type);
            }

            if (types.Count > 2)
            {
                patterns.IsConsistent = false;
                break;
            }
        }

        return patterns;
    }

    private async Task<SampleData> ExtractStratifiedSampleAsync(Worksheet ws)
    {
        await Task.CompletedTask;

        var structure = await AnalyzeStructureAsync(ws);
        var sample = new SampleData();

        for (int c = structure.FirstColumn; c <= structure.LastColumn; c++)
        {
            sample.Headers.Add(ws.Cells[structure.HeaderRow, c].StringValue.Trim());
        }

        var indices = new HashSet<int>();

        // First N rows
        for (int r = structure.FirstDataRow;
             r < structure.FirstDataRow + _sampleRows && r <= structure.LastDataRow;
             r++)
            indices.Add(r);

        // Last N rows
        for (int r = Math.Max(structure.FirstDataRow, structure.LastDataRow - _sampleRows + 1);
             r <= structure.LastDataRow;
             r++)
            indices.Add(r);

        // Random N from middle
        var remaining = Enumerable.Range(structure.FirstDataRow, structure.LastDataRow - structure.FirstDataRow + 1)
            .Except(indices)
            .ToList();

        if (remaining.Count > 0)
        {
            var rnd = new Random();
            foreach (var r in remaining.OrderBy(_ => rnd.Next()).Take(Math.Min(_sampleRows, remaining.Count)))
                indices.Add(r);
        }

        foreach (var r in indices.OrderBy(i => i))
        {
            var row = new Dictionary<string, string>();
            for (int c = structure.FirstColumn; c <= structure.LastColumn; c++)
            {
                var header = sample.Headers[c - structure.FirstColumn];
                row[header] = ws.Cells[r, c].StringValue.Trim();
            }

            sample.Rows.Add(row);
        }

        var sb = new StringBuilder();
        sb.AppendLine("| " + string.Join(" | ", sample.Headers) + " |");
        sb.AppendLine("| " + string.Join(" | ", sample.Headers.Select(_ => "---")) + " |");

        foreach (var row in sample.Rows)
        {
            var cells = sample.Headers.Select(h => row.GetValueOrDefault(h, "").Replace("|", "\\|"));
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
        }

        sample.MarkdownTable = sb.ToString();

        return sample;
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

    private DataType InferCellType(Cell cell, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DataType.Empty;

        if (cell.Type == CellValueType.IsDateTime)
            return DataType.Date;

        if (cell.Type == CellValueType.IsNumeric)
            return DataType.Number;

        if (cell.IsFormula)
            return DataType.Formula;

        if (DateTime.TryParse(value, out _))
            return DataType.Date;

        if (double.TryParse(value, out _))
            return DataType.Number;

        if (value.StartsWith("="))
            return DataType.Formula;

        return DataType.Text;
    }

    private bool DetectFormulas(Worksheet ws, int column, WorksheetStructure structure)
    {
        int formulaCount = 0;
        for (int r = structure.FirstDataRow; r <= Math.Min(structure.FirstDataRow + 20, structure.LastDataRow); r++)
        {
            if (ws.Cells[r, column].IsFormula)
                formulaCount++;
        }

        return formulaCount > 5;
    }

    private string GetColumnLetter(int columnIndex)
    {
        string letter = "";
        while (columnIndex >= 0)
        {
            letter = (char)('A' + columnIndex % 26) + letter;
            columnIndex = columnIndex / 26 - 1;
        }

        return letter;
    }

    #endregion

    #region Data Models

    private class ExcelAnalysis
    {
        public string FilePath { get; set; } = "";
        public string SheetName { get; set; } = "";
        public WorksheetStructure Structure { get; set; } = new();
        public Dictionary<string, ColumnMetadata> ColumnMetadata { get; set; } = new();
        public DataPatterns DataPatterns { get; set; } = new();
        public SampleData SampleData { get; set; } = new();
    }

    private class WorksheetStructure
    {
        public StructureType Type { get; set; }
        public int HeaderRow { get; set; }
        public int FirstDataRow { get; set; }
        public int LastDataRow { get; set; }
        public int FirstColumn { get; set; }
        public int LastColumn { get; set; }
        public List<int> EmptyColumns { get; set; } = new();
        public int TotalRows { get; set; }
        public int TotalColumns { get; set; }
    }

    private class ColumnMetadata
    {
        public int Index { get; set; }
        public string Header { get; set; } = "";
        public string Letter { get; set; } = "";
        public DataType DominantType { get; set; }
        public double EmptyPercentage { get; set; }
        public List<string> SampleValues { get; set; } = new();
        public bool HasFormulas { get; set; }
        public NumericRange? NumericRange { get; set; }
    }

    private class NumericRange
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Average { get; set; }
    }

    private class DataPatterns
    {
        public List<int> SubtotalRows { get; set; } = new();
        public List<int> DateColumns { get; set; } = new();
        public bool HasMergedCells { get; set; }
        public bool IsConsistent { get; set; }
    }

    private class SampleData
    {
        public List<string> Headers { get; set; } = new();
        public List<Dictionary<string, string>> Rows { get; set; } = new();
        public string MarkdownTable { get; set; } = "";
    }

    private class EnhancedContext
    {
        public NavigationPlanResponse NavigationPlan { get; set; } = new();
        public ExcelAnalysis BaseAnalysis { get; set; } = new();
        public Dictionary<string, VerificationResult> VerificationResults { get; set; } = new();
        public string EnhancedSample { get; set; } = "";
    }

    private class FormulaResult
    {
        public bool Success { get; set; }
        public string Formula { get; set; } = "";
        public object? Value { get; set; }
        public string? Error { get; set; }
        public string Explanation { get; set; } = "";
        public int Attempts { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, VerificationResult> VerificationContext { get; set; } = new();
        public List<FormulaAttempt>? AttemptHistory { get; set; }
    }

    private class FormulaAttempt
    {
        public int AttemptNumber { get; set; }
        public string Formula { get; set; } = "";
        public string Explanation { get; set; } = "";
        public FormulaValidation? ValidationResult { get; set; }
        public string? ErrorAnalysis { get; set; }
        public string? Exception { get; set; }
    }

    private class FormulaValidation
    {
        public bool Success { get; set; }
        public object? Value { get; set; }
        public string? FormattedValue { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
    }

    private class DataAnalysisResult
    {
        public string Question { get; set; } = "";
        public bool Success { get; set; }
        public string? Formula { get; set; }
        public object? ComputedValue { get; set; }
        public string Explanation { get; set; } = "";
        public List<string> Insights { get; set; } = new();
        public double Confidence { get; set; }
    }

    private enum StructureType
    {
        SimpleTable,
        PivotTable,
        GroupedData
    }

    private enum DataType
    {
        Text,
        Number,
        Date,
        Formula,
        Empty
    }

    #endregion
}