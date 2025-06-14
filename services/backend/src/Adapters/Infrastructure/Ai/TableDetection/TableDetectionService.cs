using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Infrastructure.Ai.TableDetection;

/// <summary>
/// Implements table detection in spreadsheets using LLM analysis.
/// </summary>
public sealed class TableDetectionService(
    ILogger<TableDetectionService> logger,
    IChatCompletionService chatCompletion,
    IOptions<TableDetectionOptions> options,
    ITelemetryClient telemetry,
    IActionCostRepository costRepository)
    : ITableDetectionService
{
    private readonly ILogger<TableDetectionService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IChatCompletionService _chatCompletion =
        chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));

    private readonly TableDetectionOptions
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    private readonly ITelemetryClient _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

    private readonly IActionCostRepository _costRepository =
        costRepository ?? throw new ArgumentNullException(nameof(costRepository));

    // Few-shot examples for better detection
    private const string SystemPrompt = @"You are an expert at analyzing spreadsheet structure and detecting tables.
Given a compressed spreadsheet representation, identify all distinct tables present.

A table is defined as:
- A contiguous rectangular region of cells
- Has headers (usually in first row or column)
- Contains related data in rows/columns
- May have formatting patterns (dates, currency, percentages)

Output JSON format:
{
  ""tables"": [
    {
      ""sheet"": ""Sheet1"",
      ""top"": 1,      // Row number (1-based, as in Excel)
      ""left"": 1,     // Column number (1-based: A=1, B=2, C=3, etc.)
      ""bottom"": 100, // Row number (1-based)
      ""right"": 10,   // Column number (1-based)
      ""confidence"": 0.95,
      ""type"": ""financial"",
      ""description"": ""Monthly revenue by product""
    }
  ]
}

IMPORTANT: Use 1-based indexing for rows and columns, as displayed in Excel.
- Row 1 is the first row
- Column 1 is column A, column 2 is column B, etc.

Consider:
- Tables may have empty rows/columns as separators
- Headers might span multiple rows
- Look for patterns in formatting (currency, dates, percentages)
- Tables often have consistent data types in columns";

    public async Task<TableDetectionResult> DetectTablesAsync(
        string compressedText,
        string? prompt = null,
        CancellationToken cancellationToken = default)
    {
        var traceId = await _telemetry.StartTraceAsync("TableDetection",
            new { textLength = compressedText.Length }, cancellationToken);

        try
        {
            _logger.LogInformation("Starting table detection for compressed text of length {Length}",
                compressedText.Length);

            // Build the user prompt
            var userPrompt = BuildUserPrompt(compressedText, prompt);

            // Prepare chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemPrompt);
            chatHistory.AddUserMessage(userPrompt);

            // Configure execution settings
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.1, // Low temperature for consistent detection
                MaxTokens = 2000,
                ResponseFormat = "json_object"
            };

            // Track token usage
            var tokensBefore = GetTokenCount(SystemPrompt + userPrompt);

            // Execute LLM call with retry policy using plural API to support test stubs
            var response = await ExecuteWithRetryAsync(
                async () =>
                {
                    var messages = await _chatCompletion.GetChatMessageContentsAsync(
                        chatHistory,
                        settings,
                        kernel: null!,
                        cancellationToken);
                    return messages.FirstOrDefault() ?? new ChatMessageContent(AuthorRole.Assistant, string.Empty);
                },
                cancellationToken);

            var tokensAfter = GetTokenCount(response.Content ?? "");
            var totalTokens = tokensBefore + tokensAfter;

            // Parse response
            var detectionResult = ParseLlmResponse(response.Content ?? "");

            // Calculate cost
            var estimatedCost = CalculateCost(tokensBefore, tokensAfter);

            // Track cost
            await _costRepository.RecordActionCostAsync(
                "table_detection",
                estimatedCost,
                new { tokens = totalTokens },
                cancellationToken);

            _logger.LogInformation("Detected {TableCount} tables using {Tokens} tokens (cost: ${Cost})",
                detectionResult.Count, totalTokens, estimatedCost);

            await _telemetry.EndTraceAsync(traceId, true, cancellationToken);

            return new TableDetectionResult(
                detectionResult,
                totalTokens,
                estimatedCost,
                response.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during table detection");
            await _telemetry.EndTraceAsync(traceId, false, cancellationToken);
            throw;
        }
    }

    private string BuildUserPrompt(string compressedText, string? additionalPrompt)
    {
        var basePrompt = $@"Analyze this compressed spreadsheet and identify all tables:

{compressedText}";

        if (!string.IsNullOrWhiteSpace(additionalPrompt))
        {
            basePrompt += $"\n\nAdditional context: {additionalPrompt}";
        }

        return basePrompt;
    }

    private List<DetectedTable> ParseLlmResponse(string jsonResponse)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = JsonSerializer.Deserialize<TableDetectionResponse>(jsonResponse, options);

            if (response?.Tables == null || response.Tables.Count == 0)
            {
                _logger.LogWarning("No tables detected in LLM response");
                return new List<DetectedTable>();
            }

            // Map JSON coordinates (1-based) directly to record (remains 1-based)
            return response.Tables.Select(t => new DetectedTable(
                t.Sheet ?? "Sheet1",
                t.Top,
                t.Left,
                t.Bottom,
                t.Right,
                t.Confidence ?? 0.8,
                t.Type,
                t.Description
            )).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response as JSON: {Response}", jsonResponse);

            // Fallback: try to extract tables using regex
            return ExtractTablesFromText(jsonResponse);
        }
    }

    private List<DetectedTable> ExtractTablesFromText(string text)
    {
        var tables = new List<DetectedTable>();

        // Simple regex pattern to find table-like structures
        var pattern = @"(\w+)!\s*([A-Z]+\d+):([A-Z]+\d+)";
        var matches = Regex.Matches(text, pattern);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count >= 4)
            {
                var sheet = match.Groups[1].Value;
                var startCell = ParseCellReference(match.Groups[2].Value);
                var endCell = ParseCellReference(match.Groups[3].Value);

                if (startCell.HasValue && endCell.HasValue)
                {
                    tables.Add(new DetectedTable(
                        sheet,
                        startCell.Value.row,
                        startCell.Value.col,
                        endCell.Value.row,
                        endCell.Value.col,
                        0.5, // Lower confidence for regex extraction
                        null,
                        "Extracted from text pattern"
                    ));
                }
            }
        }

        return tables;
    }

    private (int row, int col)? ParseCellReference(string cellRef)
    {
        var match = Regex.Match(cellRef, @"([A-Z]+)(\d+)");
        if (!match.Success) return null;

        var col = 0;
        foreach (var c in match.Groups[1].Value)
        {
            col = col * 26 + (c - 'A' + 1);
        }

        if (int.TryParse(match.Groups[2].Value, out var row))
        {
            return (row, col);
        }

        return null;
    }

    private async Task<ChatMessageContent> ExecuteWithRetryAsync(
        Func<Task<ChatMessageContent>> operation,
        CancellationToken cancellationToken)
    {
        var maxRetries = _options.MaxRetries;
        var delay = TimeSpan.FromSeconds(1);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (i < maxRetries - 1)
            {
                _logger.LogWarning("LLM request failed, retrying in {Delay}s... (attempt {Attempt}/{Max})",
                    delay.TotalSeconds, i + 1, maxRetries);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // Exponential backoff
            }
        }

        return await operation(); // Last attempt, let it throw
    }

    private int GetTokenCount(string text)
    {
        // Rough estimation: ~4 chars per token for English text
        return (int)(text.Length / 4.0);
    }

    private decimal CalculateCost(int inputTokens, int outputTokens)
    {
        // Default pricing for GPT-4 (adjust based on actual model)
        var inputCostPer1K = _options.InputTokenCostPer1K;
        var outputCostPer1K = _options.OutputTokenCostPer1K;

        return (inputTokens * inputCostPer1K / 1000m) + (outputTokens * outputCostPer1K / 1000m);
    }

    // Response DTOs
    private class TableDetectionResponse
    {
        public List<TableDto>? Tables { get; set; }
    }

    private class TableDto
    {
        public string? Sheet { get; set; }
        public int Top { get; set; }
        public int Left { get; set; }
        public int Bottom { get; set; }
        public int Right { get; set; }
        public double? Confidence { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
    }
}

/// <summary>
/// Configuration options for table detection service.
/// </summary>
public class TableDetectionOptions
{
    /// <summary>
    /// Maximum number of retry attempts for LLM calls.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Cost per 1K input tokens in USD.
    /// </summary>
    public decimal InputTokenCostPer1K { get; set; } = 0.01m;

    /// <summary>
    /// Cost per 1K output tokens in USD.
    /// </summary>
    public decimal OutputTokenCostPer1K { get; set; } = 0.03m;

    /// <summary>
    /// Maximum context length for the model.
    /// </summary>
    public int MaxContextLength { get; set; } = 128000;
}