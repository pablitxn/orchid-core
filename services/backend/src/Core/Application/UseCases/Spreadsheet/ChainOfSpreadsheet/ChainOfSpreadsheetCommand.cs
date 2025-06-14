using Application.UseCases.Spreadsheets.CompressWorkbook;
using MediatR;

namespace Application.UseCases.Spreadsheet.ChainOfSpreadsheet;

/// <summary>
/// Command to execute Chain of Spreadsheet (CoS) for natural language QA.
/// </summary>
public sealed record ChainOfSpreadsheetCommand(
    string FilePath,
    string Question,
    CompressionStrategy CompressionStrategy = CompressionStrategy.None,
    bool IncludeReasoningTrace = true) : IRequest<ChainOfSpreadsheetResponse>;

/// <summary>
/// Response from Chain of Spreadsheet execution.
/// </summary>
public sealed record ChainOfSpreadsheetResponse(
    bool Success,
    string? Answer,
    string? DetectedTable,
    ReasoningTrace? Trace,
    string? Error = null);

/// <summary>
/// Detailed reasoning trace for auditing and debugging.
/// </summary>
public sealed record ReasoningTrace(
    TableDetectionTrace TableDetection,
    QuestionAnsweringTrace QuestionAnswering,
    TimeSpan TotalDuration,
    decimal TotalCost);

/// <summary>
/// Trace information for table detection phase.
/// </summary>
public sealed record TableDetectionTrace(
    string CompressedText,
    string LlmPrompt,
    string LlmResponse,
    int TablesDetected,
    TimeSpan Duration,
    int TokensUsed,
    decimal Cost);

/// <summary>
/// Trace information for question answering phase.
/// </summary>
public sealed record QuestionAnsweringTrace(
    string TableContext,
    string LlmPrompt,
    string LlmResponse,
    TimeSpan Duration,
    int TokensUsed,
    decimal Cost);