using System.Diagnostics;
using Application.Interfaces;
using Application.UseCases.Spreadsheet.ChainOfSpreadsheet;
using Application.UseCases.Spreadsheets.CompressWorkbook;
using MediatR;
using LogLevel = Application.Interfaces.LogLevel;

namespace Infrastructure.Telemetry.Ai.Spreadsheet;

/// <summary>
/// MediatR pipeline behavior for detailed spreadsheet operation telemetry.
/// </summary>
public sealed class SpreadsheetTelemetryBehavior<TRequest, TResponse>(
    ITelemetryClient telemetry,
    ISafeLogger safeLogger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITelemetryClient _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly ISafeLogger _safeLogger = safeLogger ?? throw new ArgumentNullException(nameof(safeLogger));


    private readonly ActivitySource _activitySource = new("Spreadsheet.Operations", "1.0.0");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;

        // Skip if not a spreadsheet-related operation
        if (!IsSpreadsheetOperation(request))
        {
            return await next();
        }

        using var activity = _activitySource.StartActivity($"Spreadsheet.{requestType}");

        // Add base attributes
        activity?.SetTag("spreadsheet.operation", requestType);
        activity?.SetTag("spreadsheet.timestamp", DateTime.UtcNow.ToString("O"));

        // Add request-specific attributes
        AddRequestSpecificTags(activity, request);

        var stopwatch = Stopwatch.StartNew();
        string? traceId = null;
        string? spanId = null;

        try
        {
            // Start Langfuse trace
            traceId = await _telemetry.StartTraceAsync(
                $"Spreadsheet_{requestType}",
                new { operation = requestType },
                cancellationToken);

            // Start operation span
            spanId = await _telemetry.StartSpanAsync(
                traceId,
                "Execute",
                GetSpanMetadata(request),
                cancellationToken);

            // Log safe operation start
            _safeLogger.LogSafe(LogLevel.Information,
                "Starting spreadsheet operation {Operation}", requestType);

            // Execute the handler
            var response = await next();

            stopwatch.Stop();

            // Add response metrics
            AddResponseMetrics(activity, response, stopwatch.Elapsed);

            // End spans successfully
            if (spanId != null!)
                await _telemetry.EndSpanAsync(traceId, spanId, true, cancellationToken: cancellationToken);

            await _telemetry.EndTraceAsync(traceId, true, cancellationToken: cancellationToken);

            // Log safe completion
            _safeLogger.LogSafe(LogLevel.Information,
                "Completed spreadsheet operation {Operation} in {Duration}ms",
                requestType, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // activity?.RecordException(ex);

            // End spans as failed
            if (spanId != null && traceId != null)
                await _telemetry.EndSpanAsync(traceId, spanId, false, cancellationToken: cancellationToken);

            if (traceId != null)
                await _telemetry.EndTraceAsync(traceId, false, cancellationToken: cancellationToken);

            // Log safe error
            _safeLogger.LogSafeError(ex,
                "Failed spreadsheet operation {Operation} after {Duration}ms",
                requestType, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private bool IsSpreadsheetOperation(TRequest request)
    {
        return request switch
        {
            CompressWorkbookCommand => true,
            ChainOfSpreadsheetCommand => true,
            _ => request.GetType().Namespace?.Contains("Spreadsheet") ?? false
        };
    }

    private void AddRequestSpecificTags(Activity? activity, TRequest request)
    {
        if (activity == null) return;

        switch (request)
        {
            case CompressWorkbookCommand compress:
                activity.SetTag("spreadsheet.file_path", _safeLogger.RedactSensitiveData(compress.FilePath));
                activity.SetTag("spreadsheet.compression_strategy", compress.Strategy.ToString());
                activity.SetTag("spreadsheet.include_formatting", compress.IncludeFormatting);
                activity.SetTag("spreadsheet.include_formulas", compress.IncludeFormulas);
                break;

            case ChainOfSpreadsheetCommand chain:
                activity.SetTag("spreadsheet.file_path", _safeLogger.RedactSensitiveData(chain.FilePath));
                activity.SetTag("spreadsheet.question_length", chain.Question.Length);
                activity.SetTag("spreadsheet.compression_strategy", chain.CompressionStrategy.ToString());
                activity.SetTag("spreadsheet.include_trace", chain.IncludeReasoningTrace);
                break;
        }
    }

    private void AddResponseMetrics(Activity? activity, TResponse response, TimeSpan duration)
    {
        if (activity == null) return;

        activity.SetTag("spreadsheet.duration_ms", duration.TotalMilliseconds);

        switch (response)
        {
            case CompressWorkbookResult compress:
                activity.SetTag("spreadsheet.original_cells", compress.Statistics.OriginalCellCount);
                activity.SetTag("spreadsheet.compressed_cells", compress.Statistics.CompressedCellCount);
                activity.SetTag("spreadsheet.compression_ratio", compress.Statistics.CompressionRatio);
                activity.SetTag("spreadsheet.estimated_tokens", compress.EstimatedTokens);
                activity.SetTag("spreadsheet.sheets_processed", compress.Statistics.SheetsProcessed);
                break;

            case ChainOfSpreadsheetResponse { Success: true, Trace: not null } chain:
                activity.SetTag("spreadsheet.tables_detected", chain.Trace.TableDetection.TablesDetected);
                activity.SetTag("spreadsheet.total_tokens",
                    chain.Trace.TableDetection.TokensUsed + chain.Trace.QuestionAnswering.TokensUsed);
                activity.SetTag("spreadsheet.total_cost_usd", chain.Trace.TotalCost);
                activity.SetTag("spreadsheet.detection_duration_ms",
                    chain.Trace.TableDetection.Duration.TotalMilliseconds);
                activity.SetTag("spreadsheet.answering_duration_ms",
                    chain.Trace.QuestionAnswering.Duration.TotalMilliseconds);
                break;
        }
    }

    private object GetSpanMetadata(TRequest request)
    {
        return request switch
        {
            CompressWorkbookCommand compress => new
            {
                workbookId = GetWorkbookId(compress.FilePath),
                strategy = compress.Strategy.ToString(),
                targetTokens = compress.TargetTokenLimit
            },
            ChainOfSpreadsheetCommand chain => new
            {
                workbookId = GetWorkbookId(chain.FilePath),
                questionHash = GetQuestionHash(chain.Question),
                compressionStrategy = chain.CompressionStrategy.ToString()
            },
            _ => new { requestType = typeof(TRequest).Name }
        };
    }

    private string GetWorkbookId(string filePath)
    {
        // Extract a safe workbook identifier from the file path
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return _safeLogger.RedactSensitiveData(fileName);
    }

    private static string GetQuestionHash(string question)
    {
        // Create a hash of the question for tracking without exposing content
        var bytes = System.Text.Encoding.UTF8.GetBytes(question);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash)[..16];
    }
}