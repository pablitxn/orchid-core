using System.Diagnostics;
using System.Text;
using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Application.UseCases.Spreadsheets.CompressWorkbook;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Application.UseCases.Spreadsheet.ChainOfSpreadsheet;

/// <summary>
/// Implements Chain of Spreadsheet (CoS) for multiphase spreadsheet QA
/// Phase 1: Compress spreadsheet and detect relevant tables
/// Phase 2: Answer question using detected table context
/// </summary>
public sealed class ChainOfSpreadsheetHandler(
    ILogger<ChainOfSpreadsheetHandler> logger,
    IMediator mediator,
    ITableDetectionService tableDetection,
    IChatCompletionService chatCompletion,
    ITelemetryClient telemetry,
    IActivityPublisher activity,
    IActionCostRepository costRepository,
    IEnhancedWorkbookLoader workbookLoader)
    : IRequestHandler<ChainOfSpreadsheetCommand, ChainOfSpreadsheetResponse>
{
    private readonly ILogger<ChainOfSpreadsheetHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    private readonly ITableDetectionService _tableDetection =
        tableDetection ?? throw new ArgumentNullException(nameof(tableDetection));

    private readonly IChatCompletionService _chatCompletion =
        chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));

    private readonly ITelemetryClient _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly IActivityPublisher _activity = activity ?? throw new ArgumentNullException(nameof(activity));

    private readonly IActionCostRepository _costRepository =
        costRepository ?? throw new ArgumentNullException(nameof(costRepository));

    private readonly IEnhancedWorkbookLoader _workbookLoader =
        workbookLoader ?? throw new ArgumentNullException(nameof(workbookLoader));

    public async Task<ChainOfSpreadsheetResponse> Handle(
        ChainOfSpreadsheetCommand request,
        CancellationToken cancellationToken)
    {
        var overallStopwatch = Stopwatch.StartNew();
        var traceId = await _telemetry.StartTraceAsync("ChainOfSpreadsheet",
            new { request.FilePath, request.Question }, cancellationToken);

        try
        {
            _logger.LogInformation("Starting Chain of Spreadsheet for question: {Question}", request.Question);

            // Phase 1: Compress spreadsheet
            var compressionStopwatch = Stopwatch.StartNew();
            var compressedResult = await CompressSpreadsheetAsync(request, cancellationToken);
            compressionStopwatch.Stop();

            if (!compressedResult.Success)
            {
                return new ChainOfSpreadsheetResponse(
                    Success: false,
                    Answer: null,
                    DetectedTable: null,
                    Trace: null,
                    Error: "Failed to compress spreadsheet");
            }

            // Phase 2: Detect relevant tables
            var detectionStopwatch = Stopwatch.StartNew();
            var detectionResult = await _tableDetection.DetectTablesAsync(
                compressedResult.CompressedContent,
                $"Focus on tables that might contain information relevant to: {request.Question}",
                cancellationToken);
            detectionStopwatch.Stop();

            var tableDetectionTrace = new TableDetectionTrace(
                compressedResult.CompressedContent,
                BuildTableDetectionPrompt(compressedResult.CompressedContent, request.Question),
                detectionResult.RawLlmResponse ?? "",
                detectionResult.Tables.Count,
                detectionStopwatch.Elapsed,
                detectionResult.TokensUsed,
                detectionResult.EstimatedCost);

            // Phase 3: Select most relevant table
            var selectedTable = SelectMostRelevantTable(detectionResult.Tables /*, request.Question*/);

            if (selectedTable == null)
            {
                _logger.LogWarning("No relevant tables found for question: {Question}", request.Question);

                // Fallback: Try to answer using the entire compressed content
                return await AnswerWithFullContextAsync(
                    request,
                    compressedResult.CompressedContent,
                    tableDetectionTrace,
                    overallStopwatch.Elapsed,
                    cancellationToken);
            }

            // Phase 4: Extract table content
            var tableContent = await ExtractTableContentAsync(
                request.FilePath,
                selectedTable,
                cancellationToken);

            // Phase 5: Answer question using table context
            var answeringStopwatch = Stopwatch.StartNew();
            var answerResult = await AnswerQuestionAsync(
                request.Question,
                tableContent,
                selectedTable,
                cancellationToken);
            answeringStopwatch.Stop();

            var questionAnsweringTrace = new QuestionAnsweringTrace(
                tableContent,
                answerResult.Prompt,
                answerResult.RawResponse,
                answeringStopwatch.Elapsed,
                answerResult.TokensUsed,
                answerResult.Cost);

            // Calculate total cost
            var totalCost = tableDetectionTrace.Cost + questionAnsweringTrace.Cost;

            // Create reasoning trace
            var trace = request.IncludeReasoningTrace
                ? new ReasoningTrace(
                    tableDetectionTrace,
                    questionAnsweringTrace,
                    overallStopwatch.Elapsed,
                    totalCost)
                : null;

            // Log activity
            await _activity.PublishAsync("chain_of_spreadsheet", new
            {
                request.Question,
                request.FilePath,
                TablesDetected = detectionResult.Tables.Count,
                SelectedTable = selectedTable.GetA1Range(),
                answerResult.Answer,
                TotalDuration = overallStopwatch.Elapsed.TotalMilliseconds,
                TotalCost = totalCost
            }, cancellationToken);

            await _telemetry.EndTraceAsync(traceId, true, cancellationToken: cancellationToken);

            return new ChainOfSpreadsheetResponse(
                Success: true,
                Answer: answerResult.Answer,
                DetectedTable: selectedTable.GetA1Range(),
                Trace: trace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Chain of Spreadsheet execution");
            await _telemetry.EndTraceAsync(traceId, false, cancellationToken: cancellationToken);

            return new ChainOfSpreadsheetResponse(
                Success: false,
                Answer: null,
                DetectedTable: null,
                Trace: null,
                Error: ex.Message);
        }
    }

    private async Task<CompressWorkbookResult> CompressSpreadsheetAsync(
        ChainOfSpreadsheetCommand request,
        CancellationToken cancellationToken)
    {
        var compressCommand = new CompressWorkbookCommand
        {
            FilePath = request.FilePath,
            Strategy = request.CompressionStrategy,
            IncludeFormatting = true,
            IncludeFormulas = false
        };

        return await _mediator.Send(compressCommand, cancellationToken);
    }

    private static string BuildTableDetectionPrompt(string compressedContent, string question)
    {
        return $"""
                Analyze this compressed spreadsheet and identify tables relevant to the question: '{question}'

                Compressed content:
                {compressedContent}
                """;
    }

    private static DetectedTable? SelectMostRelevantTable(List<DetectedTable> tables /*string question*/)
    {
        if (tables.Count == 0) return null;

        // Simple heuristic: select table with the highest confidence
        // In a more sophisticated implementation, we could use the question
        // to score relevance of each table
        return tables.OrderByDescending(t => t.ConfidenceScore).FirstOrDefault();
    }

    private async Task<string> ExtractTableContentAsync(
        string filePath,
        DetectedTable table,
        CancellationToken cancellationToken)
    {
        // Load the workbook to extract specific table range
        var workbook = await _workbookLoader.LoadAsync(filePath, cancellationToken: cancellationToken);

        var worksheet = workbook.Worksheets.FirstOrDefault(w => w.Name == table.SheetName);
        if (worksheet == null)
        {
            _logger.LogWarning("Worksheet {SheetName} not found", table.SheetName);
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Table: {table.GetA1Range()}");
        if (!string.IsNullOrEmpty(table.Description))
        {
            sb.AppendLine($"Description: {table.Description}");
        }

        sb.AppendLine("Content:");

        // Extract cells within the table range
        var cells = worksheet.Cells.Values
            .Where(c => c.RowIndex >= table.TopRow &&
                        c.RowIndex <= table.BottomRow &&
                        c.ColumnIndex >= table.LeftColumn &&
                        c.ColumnIndex <= table.RightColumn)
            .OrderBy(c => c.RowIndex)
            .ThenBy(c => c.ColumnIndex);

        foreach (var cell in cells)
        {
            var address = cell.Address;
            var value = cell.FormattedValue;
            sb.AppendLine($"{address}: {value}");
        }

        return sb.ToString();
    }

    private async Task<AnswerResult> AnswerQuestionAsync(
        string question,
        string tableContent,
        DetectedTable table,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Based on the following table data, answer this question: {question}

Table Location: {table.GetA1Range()}
{tableContent}

Provide a clear, concise answer based only on the data shown. If the data doesn't contain the answer, say so.";

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(
            "You are a helpful assistant that answers questions about spreadsheet data accurately and concisely.");
        chatHistory.AddUserMessage(prompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.1,
            MaxTokens = 500
        };

        var tokensBefore = GetTokenCount(prompt);
        var response =
            await _chatCompletion.GetChatMessageContentAsync(chatHistory, settings,
                cancellationToken: cancellationToken);
        var tokensAfter = GetTokenCount(response.Content ?? "");

        var totalTokens = tokensBefore + tokensAfter;
        var cost = CalculateCost(tokensBefore, tokensAfter);

        // Record cost
        await _costRepository.RecordActionCostAsync(
            "chain_of_spreadsheet_answer",
            cost,
            new { question, tableRange = table.GetA1Range(), tokens = totalTokens },
            cancellationToken);

        return new AnswerResult(
            response.Content ?? "Unable to generate answer",
            prompt,
            response.Content ?? "",
            totalTokens,
            cost);
    }

    private async Task<ChainOfSpreadsheetResponse> AnswerWithFullContextAsync(
        ChainOfSpreadsheetCommand request,
        string compressedContent,
        TableDetectionTrace tableDetectionTrace,
        TimeSpan elapsedSoFar,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Based on the following spreadsheet data, answer this question: {request.Question}

{compressedContent}

Provide a clear, concise answer based only on the data shown. If the data doesn't contain the answer, say so.";

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(
            "You are a helpful assistant that answers questions about spreadsheet data accurately and concisely.");
        chatHistory.AddUserMessage(prompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.1,
            MaxTokens = 500
        };

        var answeringStopwatch = Stopwatch.StartNew();
        var tokensBefore = GetTokenCount(prompt);
        var response =
            await _chatCompletion.GetChatMessageContentAsync(chatHistory, settings,
                cancellationToken: cancellationToken);
        var tokensAfter = GetTokenCount(response.Content ?? "");
        answeringStopwatch.Stop();

        var totalTokens = tokensBefore + tokensAfter;
        var cost = CalculateCost(tokensBefore, tokensAfter);

        var questionAnsweringTrace = new QuestionAnsweringTrace(
            compressedContent,
            prompt,
            response.Content ?? "",
            answeringStopwatch.Elapsed,
            totalTokens,
            cost);

        var totalCost = tableDetectionTrace.Cost + cost;

        // Record cost
        await _costRepository.RecordActionCostAsync(
            "chain_of_spreadsheet_full_context_answer",
            totalCost,
            new { request.Question, context = "full_spreadsheet", tokens = totalTokens },
            cancellationToken);

        var trace = request.IncludeReasoningTrace
            ? new ReasoningTrace(
                tableDetectionTrace,
                questionAnsweringTrace,
                elapsedSoFar + answeringStopwatch.Elapsed,
                totalCost)
            : null;

        return new ChainOfSpreadsheetResponse(
            Success: true,
            Answer: response.Content ?? "Unable to generate answer",
            DetectedTable: "Full spreadsheet context",
            Trace: trace);
    }

    private static int GetTokenCount(string text)
    {
        // Rough estimation: ~4 chars per token
        return (int)(text.Length / 4.0);
    }

    private static decimal CalculateCost(int inputTokens, int outputTokens)
    {
        // Default GPT-4 pricing
        const decimal inputCostPer1K = 0.01m;
        const decimal outputCostPer1K = 0.03m;

        return (inputTokens * inputCostPer1K / 1000m) + (outputTokens * outputCostPer1K / 1000m);
    }

    private record AnswerResult(
        string Answer,
        string Prompt,
        string RawResponse,
        int TokensUsed,
        decimal Cost);
}