using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Aspose.Cells;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;

namespace Infrastructure.Ai.SemanticKernel.Services;

/// <summary>
/// Service for analyzing spreadsheet data with dynamic format recognition and iterative context gathering
/// </summary>
public class SpreadsheetAnalysisService(
    ILogger<SpreadsheetAnalysisService> logger,
    IChatCompletionService chatCompletion,
    IActivityPublisher activityPublisher)
    : ISpreadsheetAnalysisService
{
    private readonly ILogger<SpreadsheetAnalysisService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IChatCompletionService _chatCompletion =
        chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));

    private readonly IActivityPublisher _activityPublisher =
        activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));

    private const int SampleSize = 50;
    private const int HeaderDetectionRows = 30;
    private const int MaxIterations = 10;
    private const double CoverageThreshold = 0.8;
    private const int MaxInitSampleColumns = 50;


    /// <summary>
    /// Context snapshot for each iteration of the analysis loop
    /// </summary>
    public class ContextSnapshot
    {
        public int RowIndex { get; set; }
        public int ColIndex { get; set; }
        public DocumentFormat FormatType { get; init; }
        public bool RequiredHeadersSatisfied { get; set; }
        public List<string> CollectedHeaders { get; init; } = [];
        public List<string> MissingHeaders { get; set; } = [];
        public Dictionary<string, CellStatsSummary> CellStats { get; init; } = new();
        public string ArtifactDigest { get; set; } = "";
        public double CoveragePercent { get; set; }
        public int IterationCount { get; set; }
    }

    /// <summary>
    /// Summary statistics for inspected cells
    /// </summary>
    public class CellStatsSummary
    {
        public List<string> SampleValues { get; set; } = [];
        public string InferredType { get; set; } = "";
        public string ValueRange { get; set; } = "";
        public int NonNullCount { get; set; }
    }


    /// <summary>
    /// Analyzing spreadsheet data with dynamic format recognition and iterative context gathering
    /// </summary>
    public async Task<QueryAnalysisResult> AnalyzeQueryAsync(
        string query,
        DocumentMetadata metadata,
        Worksheet worksheet,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting dynamic query analysis: {Query}", query);
        _logger.LogInformation(
            "Dataset structure - Headers at row: {HeaderRow}, Data from row {DataStart} to {DataEnd}, Total data rows: {DataRows}",
            metadata.DataStartRow - 1, metadata.DataStartRow, metadata.TotalRows - 1, metadata.DataRowCount);

        // Initialize variables for the analysis loop
        var artifacts = new List<string>();
        var currentRowIndex = 0;
        var iterationCount = 0;
        var continueAnalysis = true;
        var userIntentWithContext = "";

        // Use the data start row from metadata
        currentRowIndex = metadata.DataStartRow;

        // Iterative analysis loop
        while (continueAnalysis && iterationCount < MaxIterations && currentRowIndex <= worksheet.Cells.MaxRow)
        {
            iterationCount++;

            // Build markdown table with current batch of rows
            var markdownTable = BuildMarkdownTable(worksheet, metadata, currentRowIndex, SampleSize);

            // Analyze this batch with LLM
            var batchResult = await AnalyzeBatchWithLLMAsync(
                query,
                metadata,
                markdownTable,
                artifacts,
                iterationCount,
                cancellationToken);

            // Update artifacts with new findings
            if (!string.IsNullOrWhiteSpace(batchResult.NewArtifacts))
            {
                artifacts.Add(batchResult.NewArtifacts);
            }

            // Update user intent if provided
            if (!string.IsNullOrWhiteSpace(batchResult.UserIntentWithContext))
            {
                userIntentWithContext = batchResult.UserIntentWithContext;
            }

            // Check if we should continue
            continueAnalysis = batchResult.ContinueSnapshot;

            // Move to next batch
            currentRowIndex += SampleSize;

            await _activityPublisher.PublishAsync("spreadsheet_analysis.batch_complete", new
            {
                iteration = iterationCount,
                rowsProcessed = Math.Min(currentRowIndex, worksheet.Cells.MaxRow + 1),
                totalRows = worksheet.Cells.MaxRow + 1,
                dataRows = metadata.DataRowCount,
                artifactsCount = artifacts.Count,
                continueAnalysis,
                userIntentWithContext = batchResult.UserIntentWithContext
            });
        }

        // Combine all artifacts into a single artifact document
        var combinedArtifacts = string.Join("\n\n---\n\n", artifacts);

        // Generate final analysis result based on collected artifacts
        var finalResult = await GenerateExecutionPlanFromArtifactsAsync(
            query,
            userIntentWithContext,
            combinedArtifacts,
            metadata,
            cancellationToken);

        return finalResult;
    }

    /// <summary>
    /// Detects the document format using stratified sampling
    /// </summary>
    public async Task<DocumentFormat> DetectDocumentFormatAsync(
        Worksheet worksheet,
        CancellationToken cancellationToken)
    {
        var maxRow = worksheet.Cells.MaxRow;
        var maxCol = worksheet.Cells.MaxColumn;

        // Stratified sampling: first 50, middle 50, last 50 rows
        var samples = new List<string>();

        // First 50 rows
        for (int row = 0; row < Math.Min(SampleSize, maxRow + 1); row++)
        {
            var rowData = new List<string>();
            for (int col = 0; col <= Math.Min(10, maxCol); col++)
            {
                rowData.Add(worksheet.Cells[row, col].StringValue);
            }

            samples.Add(string.Join("|", rowData));
        }

        // Middle 50 rows
        if (maxRow > SampleSize * 2)
        {
            var middleStart = maxRow / 2 - SampleSize / 2;
            for (int row = middleStart; row < middleStart + SampleSize; row++)
            {
                var rowData = new List<string>();
                for (int col = 0; col <= Math.Min(10, maxCol); col++)
                {
                    rowData.Add(worksheet.Cells[row, col].StringValue);
                }

                samples.Add(string.Join("|", rowData));
            }
        }

        // Last 50 rows
        if (maxRow > SampleSize)
        {
            for (int row = Math.Max(0, maxRow - SampleSize + 1); row <= maxRow; row++)
            {
                var rowData = new List<string>();
                for (int col = 0; col <= Math.Min(10, maxCol); col++)
                {
                    rowData.Add(worksheet.Cells[row, col].StringValue);
                }

                samples.Add(string.Join("|", rowData));
            }
        }

        // Use LLM to detect format
        var prompt = $"""
                      Analyze these spreadsheet samples to determine the document format.

                      Samples (first 10 columns of selected rows):
                      {string.Join("\n", samples.Take(20))}

                      Determine if this is:
                      - Columnar: Traditional format with headers in first row
                      - RowBased: Data organized by rows with headers in first column
                      - Nested: Hierarchical or pivot-like structure
                      - Matrix: Cross-tabulation format
                      - Mixed: Combination of formats
                      - Unknown: Cannot determine format

                      Look for patterns like:
                      - Consistent headers in first row/column
                      - Repeating structures
                      - Hierarchical indentation
                      - Cross-references between rows and columns
                      """;

        var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "format_detection",
            jsonSchema: BinaryData.FromString("""
                                              {
                                                  "type": "object",
                                                  "properties": {
                                                      "Format": { 
                                                          "type": "string",
                                                          "enum": ["Columnar", "RowBased", "Nested", "Matrix", "Mixed", "Unknown"]
                                                      },
                                                      "Confidence": { "type": "number" },
                                                      "Reasoning": { "type": "string" },
                                                      "HeaderLocation": { "type": "string" }
                                                  },
                                                  "required": ["Format", "Confidence", "Reasoning", "HeaderLocation"],
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

        var response = await _chatCompletion.GetChatMessageContentsAsync(
            chatHistory, settings, cancellationToken: cancellationToken);

        var result = JsonSerializer.Deserialize<FormatDetectionResult>(response[0].Content ?? "{}");

        return Enum.Parse<DocumentFormat>(result?.Format ?? "Unknown");
    }

    /// <summary>
    /// Extracts document metadata based on the detected format
    /// </summary>
    public async Task<DocumentMetadata> ExtractDocumentMetadataAsync(
        Worksheet worksheet,
        DocumentFormat format,
        List<HeaderInfo> headers,
        CancellationToken cancellationToken)
    {
        // Get header row index from the first header (all headers should be on the same row)
        var headerRowIndex = headers.Any() ? headers.First().RowIndex : 0;
        var dataStartRow = headerRowIndex + 1;
        var totalRows = worksheet.Cells.MaxRow + 1;

        // Calculate data row count safely
        var dataRowCount = Math.Max(0, totalRows - dataStartRow);

        // Handle edge case where headers might be at the last row
        if (dataStartRow >= totalRows)
        {
            _logger.LogWarning(
                "Headers found at last row or beyond. No data rows available. HeaderRow: {HeaderRow}, TotalRows: {TotalRows}",
                headerRowIndex, totalRows);
            dataRowCount = 0;
        }

        var metadata = new DocumentMetadata
        {
            Format = format,
            TotalRows = totalRows,
            TotalColumns = worksheet.Cells.MaxColumn + 1,
            DataStartRow = dataStartRow,
            DataRowCount = dataRowCount
        };

        // Use the headers passed as parameter
        metadata.Headers = headers.Select(h => h.Name).ToList();

        // Detect data types for each identified header/column
        metadata.DataTypes = await DetectDataTypesAsync(worksheet, metadata.Headers, format, cancellationToken);

        return metadata;
    }

    /// <summary>
    /// Creates a markdown table representation of worksheet data with real row indices
    /// </summary>
    private string CreateMarkdownTableWithRealIndices(Worksheet worksheet, int startRow, int endRow,
        int? maxCols = null)
    {
        var sb = new StringBuilder();
        var actualMaxRow = worksheet.Cells.MaxRow;
        var actualMaxCol = worksheet.Cells.MaxColumn;

        // Validate and adjust row indices
        startRow = Math.Max(0, Math.Min(startRow, actualMaxRow));
        endRow = Math.Max(startRow, Math.Min(endRow, actualMaxRow));

        // Handle edge case where worksheet might be empty
        if (actualMaxRow < 0 || actualMaxCol < 0)
        {
            return "*Empty worksheet*";
        }

        var colsToShow = maxCols.HasValue ? Math.Min(maxCols.Value, actualMaxCol) : actualMaxCol;

        // Table header
        sb.Append("| Row # |");
        for (int col = 0; col <= colsToShow; col++)
        {
            sb.Append($" Col {col} |");
        }

        if (colsToShow < actualMaxCol)
        {
            sb.Append(" ... |");
        }

        sb.AppendLine();

        // Separator
        sb.Append("|-------|");
        for (int col = 0; col <= colsToShow; col++)
        {
            sb.Append("--------|");
        }

        if (colsToShow < actualMaxCol)
        {
            sb.Append("-----|");
        }

        sb.AppendLine();

        // Data rows with actual row indices
        var rowsShown = 0;
        for (int row = startRow; row <= endRow; row++)
        {
            sb.Append($"| {row} |");
            for (int col = 0; col <= colsToShow; col++)
            {
                var cellValue = "";
                try
                {
                    cellValue = worksheet.Cells[row, col].StringValue ?? "";
                }
                catch
                {
                    // Handle any access errors gracefully
                    cellValue = "";
                }

                // Limit cell content length and escape markdown
                cellValue = cellValue.Replace("|", "\\|").Replace("\n", " ").Trim();
                if (cellValue.Length > 30)
                {
                    cellValue = cellValue.Substring(0, 27) + "...";
                }

                sb.Append($" {cellValue} |");
            }

            if (colsToShow < actualMaxCol)
            {
                sb.Append(" ... |");
            }

            sb.AppendLine();
            rowsShown++;
        }

        // Add summary information
        if (rowsShown == 0)
        {
            sb.AppendLine("*No data rows in specified range*");
        }

        if (colsToShow < actualMaxCol)
        {
            sb.AppendLine($"*Note: Showing first {colsToShow + 1} columns of {actualMaxCol + 1} total columns*");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Uses LLM to extract headers from sample data
    /// </summary>
    private async Task<List<HeaderInfo>> ExtractHeadersWithLlmAsync(
        Worksheet worksheet,
        int maxSampleRows,
        CancellationToken cancellationToken)
    {
        var actualMaxRow = worksheet.Cells.MaxRow;
        var totalRows = actualMaxRow + 1;

        // Adjust sample size based on document size
        var rowsToShow = Math.Min(maxSampleRows - 1, actualMaxRow);

        // Create markdown table with real row indices
        var markdownTable = CreateMarkdownTableWithRealIndices(worksheet, 0, rowsToShow);

        var prompt = $"""
                      Analyze this spreadsheet data and identify the column headers.

                      IMPORTANT: The row numbers shown are the ACTUAL row indices from the Excel file.
                      Total rows in document: {totalRows}

                      {markdownTable}

                      Identify:
                      1. Which row contains the headers (look for the row with column names, not data)
                      2. Extract all column headers from that row
                      3. Return the ACTUAL row index where headers are found
                      4. If headers span multiple rows, combine them appropriately
                      5. Handle empty columns by using "Column_X" where X is the column index

                      Common patterns:
                      - Headers usually in row 0, but can be anywhere
                      - Look for rows with descriptive text that don't contain numeric data
                      - Headers might have empty rows above them

                      Example: If you see headers at row 18, return HeaderRowIndex: 18

                      For small documents (< 10 rows), be extra careful to distinguish headers from data.
                      """;

        var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "header_extraction",
            jsonSchema: BinaryData.FromString("""
                                              {
                                                  "type": "object",
                                                  "properties": {
                                                      "HeaderRowIndex": { "type": "integer" },
                                                      "Headers": {
                                                          "type": "array",
                                                          "items": { "type": "string" }
                                                      },
                                                      "MultiRowHeaders": { "type": "boolean" },
                                                      "Confidence": { "type": "number" }
                                                  },
                                                  "required": ["HeaderRowIndex", "Headers", "MultiRowHeaders", "Confidence"],
                                                  "additionalProperties": false
                                              }
                                              """),
            jsonSchemaIsStrict: true
        );

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = "gpt-4o-mini",
            ResponseFormat = responseFormat,
            Temperature = 0.1
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, prompt);

        var response = await _chatCompletion.GetChatMessageContentsAsync(
            chatHistory, settings, cancellationToken: cancellationToken);

        var result = JsonSerializer.Deserialize<HeaderExtractionResult>(response[0].Content ?? "{}");

        // Convert headers to HeaderInfo list with row index
        var headerInfoList = new List<HeaderInfo>();
        if (result != null && result.Headers != null)
        {
            for (int i = 0; i < result.Headers.Count; i++)
            {
                headerInfoList.Add(new HeaderInfo(result.Headers[i], result.HeaderRowIndex));
            }
        }

        return headerInfoList;
    }

    /// <summary>
    /// Gets the next sample of data based on current position and format
    /// </summary>
    private SampleData GetNextSample(
        Worksheet worksheet,
        ContextSnapshot snapshot,
        DocumentFormat format)
    {
        var sample = new SampleData();

        if (format == DocumentFormat.Columnar)
        {
            // Sample next 50 columns starting from current position
            var startCol = snapshot.ColIndex;
            var endCol = Math.Min(startCol + SampleSize, worksheet.Cells.MaxColumn + 1);

            for (int col = startCol; col < endCol; col++)
            {
                var columnData = new List<object>();
                for (int row = 0; row <= Math.Min(100, worksheet.Cells.MaxRow); row++)
                {
                    columnData.Add(worksheet.Cells[row, col].Value);
                }

                sample.Data[$"Col_{col}"] = columnData;
            }

            snapshot.ColIndex = endCol;
        }
        else if (format == DocumentFormat.RowBased)
        {
            // Sample next 50 rows
            var startRow = snapshot.RowIndex;
            var endRow = Math.Min(startRow + SampleSize, worksheet.Cells.MaxRow + 1);

            for (int row = startRow; row < endRow; row++)
            {
                var rowData = new List<object>();
                for (int col = 0; col <= worksheet.Cells.MaxColumn; col++)
                {
                    rowData.Add(worksheet.Cells[row, col].Value);
                }

                sample.Data[$"Row_{row}"] = rowData;
            }

            snapshot.RowIndex = endRow;
        }

        // Calculate coverage
        var totalCells = (worksheet.Cells.MaxRow + 1) * (worksheet.Cells.MaxColumn + 1);
        var sampledCells = snapshot.ColIndex * Math.Min(100, worksheet.Cells.MaxRow + 1) +
                           snapshot.RowIndex * (worksheet.Cells.MaxColumn + 1);
        snapshot.CoveragePercent = Math.Min(1.0, (double)sampledCells / totalCells);

        return sample;
    }

    /// <summary>
    /// Analyzes a data sample using LLM to update context
    /// </summary>
    private async Task<LlmAnalysisResult> AnalyzeSampleWithLLMAsync(
        string query,
        DocumentMetadata metadata,
        SampleData sample,
        ContextSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var prompt = $"""
                      You are analyzing a spreadsheet to answer this query: {query}

                      Document metadata:
                      - Format: {metadata.Format}
                      - Total rows: {metadata.TotalRows}
                      - Total columns: {metadata.TotalColumns}
                      - Known headers: {string.Join(", ", metadata.Headers)}

                      Current context:
                      - Iteration: {snapshot.IterationCount}
                      - Coverage: {snapshot.CoveragePercent:P}
                      - Headers collected: {string.Join(", ", snapshot.CollectedHeaders)}
                      - Previous artifact: {snapshot.ArtifactDigest}

                      New sample data:
                      {JsonSerializer.Serialize(sample.Data, new JsonSerializerOptions { WriteIndented = true })}

                      Analyze this sample and:
                      1. Identify any new headers or data patterns
                      2. Determine if you have enough context to answer the query
                      3. Update the artifact with relevant information
                      4. Specify what additional data you need (if any)
                      5. Provide the user intent with data context
                      """;

        var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "sample_analysis",
            jsonSchema: BinaryData.FromString("""
                                              {
                                                  "type": "object",
                                                  "properties": {
                                                      "NewHeadersFound": {
                                                          "type": "array",
                                                          "items": { "type": "string" }
                                                      },
                                                      "HasSufficientContext": { "type": "boolean" },
                                                      "ArtifactContent": { "type": "string" },
                                                      "NeededData": {
                                                          "type": "array",
                                                          "items": { "type": "string" }
                                                      },
                                                      "UserIntentWithContext": { "type": "string" },
                                                      "RelevantPatterns": {
                                                          "type": "array",
                                                          "items": { "type": "string" }
                                                      },
                                                      "SuggestedFormula": { "type": "string" },
                                                      "ConfidenceScore": { "type": "number" }
                                                  },
                                                  "required": ["NewHeadersFound", "HasSufficientContext", "ArtifactContent", 
                                                             "NeededData", "UserIntentWithContext", "RelevantPatterns", 
                                                             "SuggestedFormula", "ConfidenceScore"],
                                                  "additionalProperties": false
                                              }
                                              """),
            jsonSchemaIsStrict: true
        );

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = "gpt-4o",
            ResponseFormat = responseFormat,
            Temperature = 0.2
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, prompt);

        var response = await _chatCompletion.GetChatMessageContentsAsync(
            chatHistory, settings, cancellationToken: cancellationToken);

        var result = JsonSerializer.Deserialize<LlmAnalysisResult>(response[0].Content ?? "{}");

        return result ?? new LlmAnalysisResult();
    }

    /// <summary>
    /// Updates the context snapshot with analysis results
    /// </summary>
    private void UpdateContextSnapshot(ContextSnapshot snapshot, LlmAnalysisResult analysis)
    {
        // Add new headers
        foreach (var header in analysis.NewHeadersFound)
        {
            if (!snapshot.CollectedHeaders.Contains(header))
            {
                snapshot.CollectedHeaders.Add(header);
            }
        }

        // Update missing headers
        snapshot.MissingHeaders = analysis.NeededData
            .Where(d => d.StartsWith("header:"))
            .Select(d => d.Substring(7))
            .ToList();

        // Check if required headers are satisfied
        snapshot.RequiredHeadersSatisfied = analysis.HasSufficientContext ||
                                            !snapshot.MissingHeaders.Any();

        // Update artifact digest
        snapshot.ArtifactDigest = analysis.ArtifactContent.Length > 200
            ? analysis.ArtifactContent.Substring(0, 200) + "..."
            : analysis.ArtifactContent;
    }

    /// <summary>
    /// Generates the final analysis result
    /// </summary>
    private async Task<QueryAnalysisResult> GenerateFinalAnalysisAsync(
        string query,
        string userIntentWithContext,
        string artifactContent,
        List<ContextSnapshot> snapshots,
        DocumentMetadata metadata,
        CancellationToken cancellationToken)
    {
        var prompt = $"""
                      Generate the final analysis result for this query: {query}

                      User intent with context: {userIntentWithContext}

                      Collected artifact:
                      {artifactContent}

                      Metadata:
                      - Format: {metadata.Format}
                      - Headers: {string.Join(", ", metadata.Headers)}
                      - Data types: {JsonSerializer.Serialize(metadata.DataTypes)}

                      Context evolution:
                      {JsonSerializer.Serialize(snapshots.Select(s => new
                      {
                          iteration = s.IterationCount,
                          coverage = s.CoveragePercent,
                          headers = s.CollectedHeaders.Count
                      }))}

                      Provide:
                      1. The columns needed for the calculation
                      2. Filters to apply
                      3. Aggregation type
                      4. Group by column (if needed)
                      5. Whether complex calculations are required
                      6. The calculation steps
                      """;


        var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "final_analysis",
            jsonSchema: BinaryData.FromString("""
                                              {
                                                "type": "object",
                                                "properties": {
                                                  "ColumnsNeeded": {
                                                    "type": "array",
                                                    "items": { "type": "string" }
                                                  },
                                                  "Filters": {
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
                                                  "AggregationType": { "type": "string" },
                                                  "GroupBy": { "type": ["string", "null"] },
                                                  "RequiresCalculation": { "type": "boolean" },
                                                  "CalculationSteps": {
                                                    "type": "array",
                                                    "items": { "type": "string" }
                                                  },
                                                  "RequiresFullDataset": { "type": "boolean" },
                                                  "UserIntentWithContext": { "type": "string" },
                                                  "Artifact": { "type": "string" },
                                                  "ContextSnapshots": {
                                                    "type": "array",
                                                    "items": {
                                                      "type": "object",
                                                      "properties": {
                                                        "RowIndex": { "type": "integer" },
                                                        "ColIndex": { "type": "integer" },
                                                        "FormatType": {
                                                          "type": "string",
                                                          "enum": [ "Columnar", "RowBased", "Nested", "Matrix", "Mixed", "Unknown" ]
                                                        },
                                                        "RequiredHeadersSatisfied": { "type": "boolean" },
                                                        "CollectedHeaders": {
                                                          "type": "array",
                                                          "items": { "type": "string" }
                                                        },
                                                        "MissingHeaders": {
                                                          "type": "array",
                                                          "items": { "type": "string" }
                                                        },
                                                        "CellStats": {
                                                          "type": "object",
                                                          "description": "Dictionary of cell statistics keyed by header.",
                                                          "additionalProperties": {
                                                            "type": "object",
                                                            "properties": {
                                                              "SampleValues": {
                                                                "type": "array",
                                                                "items": { "type": "string" }
                                                              },
                                                              "InferredType": { "type": "string" },
                                                              "ValueRange": { "type": "string" },
                                                              "NonNullCount": { "type": "integer" }
                                                            },
                                                            "required": [
                                                              "SampleValues",
                                                              "InferredType",
                                                              "ValueRange",
                                                              "NonNullCount"
                                                            ],
                                                            "additionalProperties": false
                                                          }
                                                        },
                                                        "ArtifactDigest": { "type": "string" },
                                                        "CoveragePercent": { "type": "number" },
                                                        "IterationCount": { "type": "integer" }
                                                      },
                                                      "required": [
                                                        "RowIndex",
                                                        "ColIndex",
                                                        "FormatType",
                                                        "RequiredHeadersSatisfied",
                                                        "CollectedHeaders",
                                                        "MissingHeaders",
                                                        "ArtifactDigest",
                                                        "CoveragePercent",
                                                        "IterationCount"
                                                      ],
                                                      "additionalProperties": false
                                                    }
                                                  }
                                                },
                                                "required": [
                                                  "ColumnsNeeded",
                                                  "Filters",
                                                  "AggregationType",
                                                  "GroupBy",
                                                  "RequiresCalculation",
                                                  "CalculationSteps",
                                                  "RequiresFullDataset",
                                                  "UserIntentWithContext",
                                                  "Artifact",
                                                  "ContextSnapshots"
                                                ],
                                                "additionalProperties": false
                                              }
                                              """),
            jsonSchemaIsStrict: true
        );

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = "gpt-4o",
            ResponseFormat = responseFormat,
            Temperature = 0.1
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, prompt);

        var response = await _chatCompletion.GetChatMessageContentsAsync(
            chatHistory, settings, cancellationToken: cancellationToken);

        var result = JsonSerializer.Deserialize<FinalAnalysisResponse>(response[0].Content ?? "{}");

        return new QueryAnalysisResult
        {
            ColumnsNeeded = result?.ColumnsNeeded ?? new List<string>(),
            Filters = result?.Filters?.Select(f => new FilterCriteria
            {
                Column = f.Column,
                Operator = f.Operator,
                Value = f.Value
            }).ToList() ?? new List<FilterCriteria>(),
            AggregationType = result?.AggregationType ?? "",
            GroupBy = result?.GroupBy,
            RequiresCalculation = result?.RequiresCalculation ?? false,
            CalculationSteps = result?.CalculationSteps ?? new List<string>(),
            RequiresFullDataset = result?.RequiresFullDataset ?? false,
            UserIntentWithContext = result?.UserIntentWithContext ?? userIntentWithContext,
            Artifact = result?.Artifact ?? artifactContent,
            ContextSnapshots = snapshots.Cast<object>().ToList()
        };
    }

    /// <summary>
    /// Detects data types for columns based on format
    /// </summary>
    private async Task<Dictionary<string, string>> DetectDataTypesAsync(
        Worksheet worksheet,
        List<string> headers,
        DocumentFormat format,
        CancellationToken cancellationToken)
    {
        var dataTypes = new Dictionary<string, string>();

        foreach (var header in headers)
        {
            // Sample values for type detection
            var sampleValues = new List<object>();

            if (format == DocumentFormat.Columnar)
            {
                var colIndex = headers.IndexOf(header);
                for (int row = 1; row <= Math.Min(100, worksheet.Cells.MaxRow); row++)
                {
                    var value = worksheet.Cells[row, colIndex].Value;
                    if (value != null) sampleValues.Add(value);
                }
            }
            // Handle other formats...

            dataTypes[header] = InferDataType(sampleValues);
        }

        return dataTypes;
    }

    /// <summary>
    /// Infers data type from sample values
    /// </summary>
    private string InferDataType(List<object> values)
    {
        if (!values.Any()) return "unknown";

        var types = new Dictionary<string, int>();

        foreach (var value in values.Take(20))
        {
            if (value is double || value is int || value is decimal)
                types["numeric"] = types.GetValueOrDefault("numeric") + 1;
            else if (value is DateTime)
                types["date"] = types.GetValueOrDefault("date") + 1;
            else if (value is bool)
                types["boolean"] = types.GetValueOrDefault("boolean") + 1;
            else
                types["text"] = types.GetValueOrDefault("text") + 1;
        }

        return types.OrderByDescending(t => t.Value).First().Key;
    }

    #region Legacy Methods (Maintained for compatibility)

    /// <inheritdoc/>
    public List<HeaderInfo> ExtractHeaders(Worksheet worksheet)
    {
        return Task.Run(async () => await ExtractHeadersAsync(worksheet)).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Extrae headers usando las primeras 50 filas y análisis con LLM
    /// </summary>
    private async Task<List<HeaderInfo>> ExtractHeadersAsync(Worksheet worksheet)
    {
        var maxRows = Math.Min(MaxInitSampleColumns, worksheet.Cells.MaxRow + 1);
        var headers = await ExtractHeadersWithLlmAsync(worksheet, maxRows, CancellationToken.None);
        return headers;
    }

    /// <inheritdoc/>
    public int CountDataRows(Worksheet worksheet)
    {
        var range = GetDataRange(worksheet);
        return range.LastRow - range.FirstRow + 1;
    }

    /// <inheritdoc/>
    public (int FirstRow, int LastRow) GetDataRange(Worksheet worksheet)
    {
        return (1, worksheet.Cells.MaxRow);
    }

    /// <inheritdoc/>
    public bool RowMatchesFilters(
        Worksheet worksheet,
        int row,
        List<HeaderInfo> headers,
        List<FilterCriteria> filters)
    {
        if (!filters.Any()) return true;

        foreach (var filter in filters)
        {
            var colIndex = headers.FindIndex(h => h.Name.Equals(filter.Column, StringComparison.OrdinalIgnoreCase));
            if (colIndex < 0) continue;

            var cellValue = worksheet.Cells[row, colIndex].Value;
            bool matches = EvaluateFilter(cellValue, filter);

            if (!matches) return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<DocumentContext> GatherDocumentContextAsync(
        Workbook workbook,
        QueryAnalysisResult analysisResult,
        CancellationToken cancellationToken = default)
    {
        // This method is maintained for backward compatibility
        // It now returns a simplified context based on the new analysis
        _logger.LogInformation("Legacy GatherDocumentContextAsync called - using simplified context");

        var context = new DocumentContext
        {
            Sheets = new Dictionary<string, SheetInfo>(),
            ColumnStatistics = new Dictionary<string, ColumnStats>(),
            DetectedPatterns = new List<DataPattern>(),
            CrossSheetRelationships = new List<Relationship>()
        };

        // Convert new analysis results to legacy format
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            var sheetInfo = new SheetInfo
            {
                Name = sheet.Name,
                RowCount = sheet.Cells.MaxRow + 1,
                ColumnCount = sheet.Cells.MaxColumn + 1,
                Headers = ExtractHeaders(sheet),
                ColumnTypes = new Dictionary<string, ColumnType>(),
                FormulaCells = new List<string>()
            };

            context.Sheets[sheet.Name] = sheetInfo;
        }

        return context;
    }

    #endregion

    /// <summary>
    /// Builds a markdown table from spreadsheet data including row and column indices
    /// </summary>
    private string BuildMarkdownTable(Worksheet worksheet, DocumentMetadata metadata, int startRow, int rowCount)
    {
        var sb = new StringBuilder();
        var actualMaxRow = worksheet.Cells.MaxRow;
        var actualMaxCol = worksheet.Cells.MaxColumn;

        // Validate start row - FIX: No modificar startRow si es válido
        if (startRow < 0) startRow = 0;
        if (startRow > actualMaxRow)
        {
            sb.AppendLine("### No more data to analyze");
            sb.AppendLine($"Requested start row {startRow} is beyond the last row {actualMaxRow}");
            return sb.ToString();
        }

        var endRow = Math.Min(startRow + rowCount - 1, actualMaxRow);

        // Handle case where there's no data to show
        if (rowCount <= 0)
        {
            sb.AppendLine("### No data requested");
            return sb.ToString();
        }

        // Limit columns for readability
        var colsToShow = Math.Min(actualMaxCol, 20);

        // Table header with column letters
        sb.AppendLine("### Data Sample");
        sb.AppendLine($"*Showing rows {startRow} to {endRow}*");
        sb.AppendLine();

        sb.Append("| Row | ");
        for (int col = 0; col <= colsToShow; col++)
        {
            var colLetter = GetColumnLetter(col);
            sb.Append($"{colLetter}");
            if (col < metadata.Headers.Count && !string.IsNullOrEmpty(metadata.Headers[col]))
            {
                var header = metadata.Headers[col].Trim();
                if (header.Length > 15) header = header.Substring(0, 12) + "...";
                sb.Append($"<br/>*{header}*");
            }

            sb.Append(" | ");
        }

        if (colsToShow < actualMaxCol)
        {
            sb.Append("... | ");
        }

        sb.AppendLine();

        // Table separator
        sb.Append("|:---:|");
        for (int col = 0; col <= colsToShow; col++)
        {
            sb.Append(":------:|");
        }

        if (colsToShow < actualMaxCol)
        {
            sb.Append(":---:|");
        }

        sb.AppendLine();

        var rowsShown = 0;
        for (int row = startRow; row <= endRow && row <= actualMaxRow; row++)
        {
            sb.Append($"| **{row}** | ");

            for (int col = 0; col <= colsToShow; col++)
            {
                var cellValue = "";
                try
                {
                    var cell = worksheet.Cells[row, col];
                    if (cell != null)
                    {
                        cellValue = cell.StringValue ?? cell.Value?.ToString() ?? "";
                    }
                }
                catch
                {
                    cellValue = "";
                }

                // Escape markdown special characters and limit cell length
                cellValue = cellValue.Replace("|", "\\|")
                    .Replace("\n", " ")
                    .Replace("\r", "")
                    .Trim();

                if (cellValue.Length > 50)
                {
                    cellValue = cellValue.Substring(0, 47) + "...";
                }

                // Empty cells show as light gray
                if (string.IsNullOrWhiteSpace(cellValue))
                {
                    cellValue = "_empty_";
                }

                sb.Append($"{cellValue} | ");
            }

            if (colsToShow < actualMaxCol)
            {
                sb.Append("... | ");
            }

            sb.AppendLine();
            rowsShown++;
        }

        sb.AppendLine();
        sb.AppendLine("### 📊 Dataset Info");
        sb.AppendLine();
        sb.AppendLine("**Structure:**");
        sb.AppendLine($"- Headers at row: **{metadata.DataStartRow - 1}**");
        sb.AppendLine($"- Data range: rows **{metadata.DataStartRow}** to **{metadata.TotalRows - 1}**");
        sb.AppendLine($"- Total data rows: **{metadata.DataRowCount:N0}** *(excluding headers)*");
        sb.AppendLine(
            $"- Total columns: **{actualMaxCol + 1}** ({GetColumnLetter(0)} to {GetColumnLetter(actualMaxCol)})");

        sb.AppendLine();
        sb.AppendLine("**Current View:**");
        sb.AppendLine($"- Showing: rows **{startRow}** to **{endRow}** ({rowsShown} rows)");

        if (colsToShow < actualMaxCol)
        {
            sb.AppendLine($"- Columns: **{colsToShow + 1}** of **{actualMaxCol + 1}** (truncated for readability)");
        }

        // Progress indicator
        var progress = ((double)(endRow - metadata.DataStartRow + 1) / metadata.DataRowCount) * 100;
        sb.AppendLine($"- Progress: **{progress:F1}%** of data analyzed");

        return sb.ToString();
    }

// Helper method to convert column index to Excel-style letter
    private string GetColumnLetter(int columnIndex)
    {
        var dividend = columnIndex + 1;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar(65 + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    /// <summary>
    /// Analyzes a batch of data using LLM
    /// </summary>
    private async Task<BatchAnalysisResult> AnalyzeBatchWithLLMAsync(
        string query,
        DocumentMetadata metadata,
        string markdownTable,
        List<string> previousArtifacts,
        int iteration,
        CancellationToken cancellationToken)
    {
        var previousArtifactsText = previousArtifacts.Any()
            ? $"\n\nSome previous artifacts collected:\n{string.Join("\n---\n", previousArtifacts.Take(3))}"
            : "";

        var prompt = $"""
                      You are analyzing spreadsheet data to answer this query: {query}

                      CRITICAL STRUCTURAL CONTEXT:
                      - Headers are located at row: {metadata.DataStartRow - 1}
                      - Data starts at row: {metadata.DataStartRow}
                      - Data ends at row: {metadata.TotalRows - 1}
                      - Total DATA rows (excluding headers): {metadata.DataRowCount}
                      - This means: Row indices {metadata.DataStartRow} through {metadata.TotalRows - 1} contain the actual data

                      Document metadata:
                      - Format: {metadata.Format}
                      - Total rows in file: {metadata.TotalRows}
                      - Total columns: {metadata.TotalColumns}
                      - Headers: {string.Join(", ", metadata.Headers)}
                      - Data types: {JsonSerializer.Serialize(metadata.DataTypes)}

                      This is iteration {iteration} of the analysis.{previousArtifactsText}

                      Current data sample:
                      {markdownTable}

                      IMPORTANT FOR CALCULATIONS:
                      - The dataset has {metadata.DataRowCount} data rows (this is what matters for percentages!)
                      - Row indices shown above are the actual Excel row numbers
                      - When calculating percentages: denominator = {metadata.DataRowCount} (NOT {metadata.TotalRows})

                      FOR PERCENTAGE QUERIES - You MUST:
                      1. Extract ALL rows that match the condition from the current batch
                      2. Keep a running count of matching rows
                      3. Remember that percentage = (matching rows / {metadata.DataRowCount}) * 100
                      4. Include these critical numbers in your UserIntentWithContext

                      Instructions:
                      1. Extract any data from this sample that is relevant to answering the user's query
                      2. Store the extracted data as "artifacts" - these should be small tables or data snippets in plain text
                      3. If the query requires multiple calculations (e.g., "average of X compared to sum of Y"), create separate artifact sections for each
                      4. Determine if you need to continue analyzing more rows or if you have sufficient data
                      5. Provide the user intent with COMPLETE structural context
                      6. For queries about percentages or proportions, ensure you collect enough data to make accurate calculations

                      Format artifacts like this:
                      ## Artifact: [Description]
                      [Data in a simple format, could be a small table or list]

                      CRITICAL for UserIntentWithContext:
                      Include ALL structural observations:
                      - "The user wants to know what percentage of rows have Quantity > 1000"
                      - "The dataset has headers at row {metadata.DataStartRow - 1} and data from rows {metadata.DataStartRow} to {metadata.TotalRows - 1}"
                      - "This gives us {metadata.DataRowCount} total data rows for percentage calculations"
                      - "I found X rows matching the condition out of {metadata.DataRowCount} total data rows"

                      Example artifact format for percentage queries:
                      ## Artifact: Rows with Quantity > 1000
                      Row | Quantity
                      {metadata.DataStartRow + 3} | 1200
                      {metadata.DataStartRow + 6} | 1500
                      ...
                      Total matching rows found: [count]
                      Total data rows in dataset: {metadata.DataRowCount}
                      Header row index: {metadata.DataStartRow - 1}
                      Data row range: {metadata.DataStartRow} to {metadata.TotalRows - 1}
                      """;

        var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "batch_analysis",
            jsonSchema: BinaryData.FromString("""
                                              {
                                                  "type": "object",
                                                  "properties": {
                                                      "NewArtifacts": {
                                                          "type": "string",
                                                          "description": "New artifacts extracted from this batch"
                                                      },
                                                      "ContinueSnapshot": {
                                                          "type": "boolean",
                                                          "description": "Whether to continue analyzing more rows"
                                                      },
                                                      "UserIntentWithContext": {
                                                          "type": "string",
                                                          "description": "The user's intent with full context from data seen so far"
                                                      },
                                                      "Reasoning": {
                                                          "type": "string",
                                                          "description": "Explanation of what was found and why to continue or stop"
                                                      }
                                                  },
                                                  "required": ["NewArtifacts", "ContinueSnapshot", "UserIntentWithContext", "Reasoning"],
                                                  "additionalProperties": false
                                              }
                                              """),
            jsonSchemaIsStrict: true
        );

        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = "o4-mini",
            ResponseFormat = responseFormat,
            Temperature = 0.2
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, prompt);

        var response = await _chatCompletion.GetChatMessageContentsAsync(
            chatHistory, settings, cancellationToken: cancellationToken);

        var result = JsonSerializer.Deserialize<BatchAnalysisResult>(response[0].Content ?? "{}");

        return result ?? new BatchAnalysisResult();
    }

    /// <summary>
    /// Generates execution plan from collected artifacts
    /// </summary>
    private async Task<QueryAnalysisResult> GenerateExecutionPlanFromArtifactsAsync(
        string query,
        string userIntentWithContext,
        string combinedArtifacts,
        DocumentMetadata metadata,
        CancellationToken cancellationToken)
    {
        var prompt = $"""
                      Generate an execution plan for this query: {query}

                      User intent with FULL STRUCTURAL CONTEXT: {userIntentWithContext}

                      Always for any mathematical calculations return `need_run_formula=true` and provide:
                        - artifacts_formatted: A structured representation of the data needed for the calculation
                        - formula: The Excel formula to execute on the dynamically created spreadsheet
                      
                      Collected artifacts from document traversal:
                      {combinedArtifacts}

                      Document metadata:
                      - Headers: {string.Join(", ", metadata.Headers)}
                      - Data types: {JsonSerializer.Serialize(metadata.DataTypes)}
                      - Total rows in file: {metadata.TotalRows}
                      - Header row index: {metadata.DataStartRow - 1}
                      - Data start row: {metadata.DataStartRow}
                      - Data end row: {metadata.TotalRows - 1}
                      - Total DATA rows (for calculations): {metadata.DataRowCount}

                      CRITICAL UNDERSTANDING:
                      - The UserIntentWithContext above contains crucial structural observations from the iterative analysis
                      - It should tell you exactly how many matching rows were found and what the total data row count is
                      - The artifacts contain the actual matching data extracted during traversal
                      - For percentage calculations: Use the counts mentioned in UserIntentWithContext!

                      Instructions:
                      1. If the query can be answered directly from the artifacts (zero math needed), provide a simple_answer
                      3. Always provide reasoning that references the structural context

                      For artifacts_formatted, structure the data as a 2D array where:
                      - First row contains headers
                      - Subsequent rows contain the data values
                      - Include only the columns needed for the calculation
                      - Ensure data types are preserved (numbers as numbers, not strings)

                      FORMULA GUIDELINES based on UserIntentWithContext:
                      - Read the UserIntentWithContext carefully - it contains the exact counts!
                      - For percentage queries where UserIntentWithContext says "I found X rows matching out of Y total data rows":
                        * Simple answer approach: Just calculate X/Y*100 directly
                        * Formula approach: =COUNTA(range_with_matches)/{metadata.DataRowCount}*100
                      - The key insight: UserIntentWithContext already did the hard work of counting!

                      Example reasoning for percentage query:
                      "Based on the UserIntentWithContext, we found X rows with Quantity > 1000 out of {metadata.DataRowCount} total data rows.
                      The percentage is therefore (X / {metadata.DataRowCount}) * 100 = Z%"

                      NEVER confuse:
                      - Total rows in file ({metadata.TotalRows}) with total data rows ({metadata.DataRowCount})
                      - Row indices with row counts
                      - The header row is at index {metadata.DataStartRow - 1}, but this doesn't affect our percentage calculation
                      """;

        var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "execution_plan",
            jsonSchema: BinaryData.FromString("""
                                              {
                                                "type": "object",
                                                "properties": {
                                                  "NeedRunFormula": {
                                                    "type": "boolean",
                                                    "description": "Always for ANY mathematical calculations return True"
                                                  },
                                                  "ArtifactsFormatted": {
                                                    "type": "array",
                                                    "description": "2D array representing the Excel data. First row is headers.",
                                                    "items": {
                                                      "type": "array",
                                                      "items": {
                                                        "type": ["string", "number", "boolean", "null"]
                                                      }
                                                    }
                                                  },
                                                  "Formula": {
                                                    "type": "string",
                                                    "description": "Excel formula to execute. Use cell references like A1, B2, etc."
                                                  },
                                                  "SimpleAnswer": {
                                                    "type": "string",
                                                    "description": "Direct answer if the query can be answered"
                                                  },
                                                  "Reasoning": {
                                                    "type": "string",
                                                    "description": "Explanation of the approach taken"
                                                  }
                                                },
                                                "required": [
                                                  "NeedRunFormula",
                                                  "ArtifactsFormatted",
                                                  "Formula",
                                                  "SimpleAnswer",
                                                  "Reasoning"
                                                ],
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

        var response = await _chatCompletion.GetChatMessageContentsAsync(
            chatHistory, settings, cancellationToken: cancellationToken);

        var result = JsonSerializer.Deserialize<ExecutionPlanResponse>(response[0].Content ?? "{}");

        // Convert the execution plan to QueryAnalysisResult
        // This is a temporary mapping until we update the return type
        return new QueryAnalysisResult
        {
            ColumnsNeeded = ExtractColumnsFromArtifacts(result?.ArtifactsFormatted),
            Filters = new List<FilterCriteria>(), // No filters in the new approach
            AggregationType = "", // Determined by formula
            GroupBy = null,
            RequiresCalculation = result?.NeedRunFormula ?? false,
            CalculationSteps = new List<string> { result?.Formula ?? "" },
            RequiresFullDataset = false,
            UserIntentWithContext = userIntentWithContext,
            Artifact = JsonSerializer.Serialize(new
            {
                ExecutionPlan = result,
                OriginalArtifacts = combinedArtifacts
            }),
            ContextSnapshots = new List<object>() // No longer using snapshots
        };
    }

    /// <summary>
    /// Extracts column names from formatted artifacts
    /// </summary>
    private List<string> ExtractColumnsFromArtifacts(List<List<object>>? artifacts)
    {
        if (artifacts == null || artifacts.Count == 0)
            return new List<string>();

        // First row contains headers
        return artifacts[0]?.Select(h => h?.ToString() ?? "").ToList() ?? new List<string>();
    }

    #region Helper Methods

    private bool EvaluateFilter(object cellValue, FilterCriteria filter)
    {
        switch (filter.Operator.ToLower())
        {
            case "equals":
                return CompareEquals(cellValue, filter.Value);

            case "contains":
                return cellValue?.ToString()?.Contains(filter.Value, StringComparison.OrdinalIgnoreCase) ?? false;

            case ">":
            case "<":
            case ">=":
            case "<=":
                if (TryParseNumeric(cellValue, out var numVal) &&
                    double.TryParse(filter.Value, out var filterNum))
                {
                    return filter.Operator switch
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
                var dateVal = TryParseDate(cellValue);
                if (dateVal.HasValue && DateTime.TryParse(filter.Value, out var filterDate))
                {
                    return filter.Operator switch
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

        return false;
    }

    private bool CompareEquals(object cellValue, string filterValue)
    {
        if (cellValue == null) return string.IsNullOrEmpty(filterValue);

        var cellStr = cellValue.ToString() ?? "";

        // Try numeric comparison first
        if (TryParseNumeric(cellValue, out var cellNum) &&
            double.TryParse(filterValue, out var filterNum))
        {
            return Math.Abs(cellNum - filterNum) < 0.0001;
        }

        // Try date comparison
        var cellDate = TryParseDate(cellValue);
        if (cellDate.HasValue && DateTime.TryParse(filterValue, out var filterDate))
        {
            return cellDate.Value.Date == filterDate.Date;
        }

        // Fall back to string comparison
        return cellStr.Equals(filterValue, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryParseNumeric(object cellValue, out double result)
    {
        result = 0;

        if (cellValue == null) return false;

        return cellValue switch
        {
            double d => (result = d, true).Item2,
            int i => (result = i, true).Item2,
            decimal dec => (result = (double)dec, true).Item2,
            string s => double.TryParse(s.Trim().Replace("$", "").Replace(",", ""), out result),
            _ => double.TryParse(cellValue.ToString(), out result)
        };
    }

    private DateTime? TryParseDate(object cellValue)
    {
        if (cellValue == null) return null;

        if (cellValue is DateTime dt) return dt;

        if (cellValue is double oaDate)
        {
            try
            {
                return DateTime.FromOADate(oaDate);
            }
            catch
            {
                return null;
            }
        }

        if (DateTime.TryParse(cellValue.ToString(), out var parsed))
            return parsed;

        return null;
    }

    #endregion

    #region DTOs

    /// <summary>
    /// Sample data extracted from worksheet
    /// </summary>
    private class SampleData
    {
        public Dictionary<string, List<object>> Data { get; set; } = new();
    }

    /// <summary>
    /// Result from LLM analysis of a sample
    /// </summary>
    private class LlmAnalysisResult
    {
        public List<string> NewHeadersFound { get; init; } = [];
        public bool HasSufficientContext { get; init; }
        public string ArtifactContent { get; init; } = "";
        public List<string> NeededData { get; init; } = [];
        public string UserIntentWithContext { get; init; } = "";
        public List<string> RelevantPatterns { get; init; } = [];
        public string SuggestedFormula { get; init; } = "";
        public double ConfidenceScore { get; init; }
    }

    /// <summary>
    /// Format detection result from LLM
    /// </summary>
    private class FormatDetectionResult
    {
        public string Format { get; init; } = "Unknown";
        public double Confidence { get; init; }
        public string Reasoning { get; init; } = "";
        public string HeaderLocation { get; init; } = "";
    }

    /// <summary>
    /// Header extraction result from LLM
    /// </summary>
    private class HeaderExtractionResult
    {
        public int HeaderRowIndex { get; init; }
        public List<string> Headers { get; init; } = [];
        public bool MultiRowHeaders { get; init; }
        public double Confidence { get; init; }
    }

    /// <summary>
    /// Final analysis response from LLM
    /// </summary>
    private class FinalAnalysisResponse
    {
        public List<string> ColumnsNeeded { get; init; } = [];
        public List<FilterResponse> Filters { get; init; } = [];
        public string AggregationType { get; init; } = "";
        public string? GroupBy { get; init; }
        public bool RequiresCalculation { get; init; }
        public List<string> CalculationSteps { get; init; } = [];
        public bool RequiresFullDataset { get; init; }
        public string UserIntentWithContext { get; init; } = "";
        public string Artifact { get; init; } = "";
        public List<object> ContextSnapshots { get; init; } = [];
    }

    private class FilterResponse
    {
        public string Column { get; set; } = "";
        public string Operator { get; set; } = "";
        public string Value { get; set; } = "";
    }

    /// <summary>
    /// Result from batch analysis
    /// </summary>
    private class BatchAnalysisResult
    {
        public string NewArtifacts { get; set; } = "";
        public bool ContinueSnapshot { get; set; }
        public string UserIntentWithContext { get; set; } = "";
        public string Reasoning { get; set; } = "";
    }

    /// <summary>
    /// Execution plan response from LLM
    /// </summary>
    private class ExecutionPlanResponse
    {
        public bool NeedRunFormula { get; set; }
        public List<List<object>> ArtifactsFormatted { get; set; } = new();
        public string Formula { get; set; } = "";
        public string SimpleAnswer { get; set; } = "";
        public string Reasoning { get; set; } = "";
    }

    #endregion
}