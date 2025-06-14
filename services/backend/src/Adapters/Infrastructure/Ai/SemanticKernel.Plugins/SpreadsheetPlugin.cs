using System.ComponentModel;
using Application.Interfaces;
using Application.UseCases.Spreadsheet.ChainOfSpreadsheet;
using Application.UseCases.Spreadsheets.CompressWorkbook;
using MediatR;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace Infrastructure.Ai.SemanticKernel.Plugins;

/// <summary>
///     Thin Semantic Kernel plugin delegating spreadsheet operations to application use cases.
/// </summary>
public sealed class SpreadsheetPlugin(IMediator mediator, IActivityPublisher activityPublisher)
{
    private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    private readonly IActivityPublisher _activityPublisher =
        activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));

    [KernelFunction("compress_spreadsheet")]
    [Description(
        "Compresses large Excel spreadsheets for efficient LLM processing while preserving structural information.")]
    public async Task<string> CompressSpreadsheetAsync(
        [Description("Path to the Excel file")]
        string filePath,
        [Description("Compression strategy: None, Balanced, or Aggressive")]
        string strategy = "None",
        [Description("Target token limit for compressed output")]
        int? targetTokenLimit = null,
        [Description("Include formatting information")]
        bool includeFormatting = false,
        [Description("Include formulas")] bool includeFormulas = true)
    {
        // Parse strategy
        if (!Enum.TryParse<CompressionStrategy>(strategy, true, out var compressionStrategy))
        {
            compressionStrategy = CompressionStrategy.None;
        }

        var command = new CompressWorkbookCommand
        {
            FilePath = filePath,
            Strategy = compressionStrategy,
            TargetTokenLimit = targetTokenLimit,
            IncludeFormatting = includeFormatting,
            IncludeFormulas = includeFormulas
        };

        var result = await _mediator.Send(command);

        // Create response object
        var response = new
        {
            compressed_content = result.CompressedContent,
            estimated_tokens = result.EstimatedTokens,
            statistics = new
            {
                original_cells = result.Statistics.OriginalCellCount,
                compressed_cells = result.Statistics.CompressedCellCount,
                compression_ratio = result.Statistics.CompressionRatio,
                sheets_processed = result.Statistics.SheetsProcessed,
                processing_time_ms = result.Statistics.ProcessingTimeMs
            },
            warnings = result.Warnings
        };

        // Publish tool invocation for telemetry
        await PublishToolAsync("compress_spreadsheet",
            new { filePath, strategy, targetTokenLimit, includeFormatting, includeFormulas },
            JsonSerializer.Serialize(response));

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    [KernelFunction("chain_of_spreadsheet")]
    [Description(
        "Uses Chain of Spreadsheet (CoS) to answer natural language questions by first detecting relevant tables, then answering based on focused context.")]
    public async Task<string> ChainOfSpreadsheetAsync(
        [Description("Path to the Excel file")]
        string filePath,
        [Description("Natural language question about the data")]
        string question,
        [Description("Compression strategy: None, Balanced, or Aggressive")]
        string compressionStrategy = "Balanced",
        [Description("Include detailed reasoning trace")]
        bool includeTrace = false)
    {
        // Parse strategy
        if (!Enum.TryParse<CompressionStrategy>(compressionStrategy, true, out var strategy))
        {
            strategy = CompressionStrategy.Balanced;
        }

        var command = new ChainOfSpreadsheetCommand(
            filePath,
            question,
            strategy,
            includeTrace);

        var result = await _mediator.Send(command);

        
        // Create response object
        var response = new
        {
            success = result.Success,
            answer = result.Answer,
            detected_table = result.DetectedTable,
            error = result.Error,
            trace = includeTrace && result.Trace != null
                ? new
                {
                    table_detection = new
                    {
                        tables_detected = result.Trace.TableDetection.TablesDetected,
                        duration_ms = result.Trace.TableDetection.Duration.TotalMilliseconds,
                        tokens = result.Trace.TableDetection.TokensUsed,
                        cost = result.Trace.TableDetection.Cost
                    },
                    question_answering = new
                    {
                        duration_ms = result.Trace.QuestionAnswering.Duration.TotalMilliseconds,
                        tokens = result.Trace.QuestionAnswering.TokensUsed,
                        cost = result.Trace.QuestionAnswering.Cost
                    },
                    total_duration_ms = result.Trace.TotalDuration.TotalMilliseconds,
                    total_cost = result.Trace.TotalCost
                }
                : null
        };

        // Publish tool invocation
        await PublishToolAsync("chain_of_spreadsheet",
            new { filePath, question, compressionStrategy, includeTrace },
            JsonSerializer.Serialize(response));

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task PublishToolAsync(string toolName, object parameters, string result)
    {
        await _activityPublisher.PublishAsync("tool_invocation",
            new { tool = toolName, parameters, result },
            CancellationToken.None);
    }
}