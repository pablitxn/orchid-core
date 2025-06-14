using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Spreadsheet.NaturalLanguageQuery;

public sealed class NaturalLanguageQueryHandler(
    ILogger<NaturalLanguageQueryHandler> logger,
    IWorkbookLoader workbookLoader,
    IWorkbookNormalizer normalizer,
    IWorkbookSummarizer summarizer,
    IFormulaTranslator translator,
    IFormulaValidator validator,
    IFormulaExecutor executor,
    ICacheStore cache,
    IActivityPublisher activity) : IRequestHandler<NaturalLanguageQueryCommand, NaturalLanguageQueryResponse>
{
    private readonly ILogger<NaturalLanguageQueryHandler> _logger = logger;
    private readonly IWorkbookLoader _workbookLoader = workbookLoader;
    private readonly IWorkbookNormalizer _normalizer = normalizer;
    private readonly IWorkbookSummarizer _summarizer = summarizer;
    private readonly IFormulaTranslator _translator = translator;
    private readonly IFormulaValidator _validator = validator;
    private readonly IFormulaExecutor _executor = executor;
    private readonly ICacheStore _cache = cache;
    private readonly IActivityPublisher _activity = activity;

    public async Task<NaturalLanguageQueryResponse> Handle(
        NaturalLanguageQueryCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing natural language query: {Query} for file: {File}", 
            request.Query, request.FilePath);

        try
        {
            // Phase 0: Load and normalize workbook
            var normalizedWorkbook = await GetNormalizedWorkbookAsync(request.FilePath, cancellationToken);
            
            // Phase 1: Summarize for LLM
            var summary = await GetWorkbookSummaryAsync(normalizedWorkbook, request.FilePath, cancellationToken);
            
            // Phase 2: Translate query to formula
            var translation = await _translator.TranslateAsync(request.Query, summary, cancellationToken);
            
            if (translation.NeedsClarification)
            {
                _logger.LogWarning("Query needs clarification: {Prompt}", translation.ClarificationPrompt);
                return new NaturalLanguageQueryResponse(
                    Success: false,
                    Result: null,
                    Formula: string.Empty,
                    Explanation: translation.Explanation,
                    NeedsClarification: true,
                    ClarificationPrompt: translation.ClarificationPrompt);
            }
            
            // Phase 3: Validate formula
            var validation = await _validator.ValidateAsync(translation.Formula, normalizedWorkbook, cancellationToken);
            
            if (!validation.IsValid)
            {
                _logger.LogWarning("Invalid formula: {Formula}. Errors: {Errors}", 
                    translation.Formula, string.Join("; ", validation.Errors));
                
                return new NaturalLanguageQueryResponse(
                    Success: false,
                    Result: null,
                    Formula: translation.Formula,
                    Explanation: translation.Explanation,
                    Error: string.Join("; ", validation.Errors));
            }
            
            // Phase 4: Execute formula
            var result = await _executor.ExecuteAsync(
                translation.Formula,
                request.FilePath,
                normalizedWorkbook.MainWorksheet.Name,
                TimeSpan.FromSeconds(30),
                cancellationToken);
            
            if (!result.Success)
            {
                _logger.LogError("Formula execution failed: {Error}", result.Error);
                return new NaturalLanguageQueryResponse(
                    Success: false,
                    Result: null,
                    Formula: translation.Formula,
                    Explanation: translation.Explanation,
                    Error: result.Error);
            }
            
            // Phase 5: Log and return result
            await LogQueryAsync(request, translation, result, cancellationToken);
            
            return new NaturalLanguageQueryResponse(
                Success: true,
                Result: FormatResult(result),
                Formula: translation.Formula,
                Explanation: translation.Explanation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing natural language query");
            return new NaturalLanguageQueryResponse(
                Success: false,
                Result: null,
                Formula: string.Empty,
                Explanation: string.Empty,
                Error: ex.Message);
        }
    }

    private async Task<NormalizedWorkbook> GetNormalizedWorkbookAsync(string filePath, CancellationToken ct)
    {
        var cacheKey = $"normalized-workbook:{filePath}";
        var cached = await _cache.GetAsync<NormalizedWorkbook>(cacheKey, ct);
        
        if (cached != null)
        {
            _logger.LogDebug("Using cached normalized workbook for {FilePath}", filePath);
            return cached;
        }
        
        var workbook = await _workbookLoader.LoadAsync(filePath, ct);
        var normalized = await _normalizer.NormalizeAsync(workbook, ct);
        
        await _cache.SetAsync(cacheKey, normalized, TimeSpan.FromHours(24), ct);
        
        return normalized;
    }

    private async Task<WorkbookSummary> GetWorkbookSummaryAsync(
        NormalizedWorkbook workbook, 
        string filePath, 
        CancellationToken ct)
    {
        var cacheKey = $"workbook-summary:{filePath}";
        var cached = await _cache.GetAsync<WorkbookSummary>(cacheKey, ct);
        
        if (cached != null)
        {
            _logger.LogDebug("Using cached workbook summary for {FilePath}", filePath);
            return cached;
        }
        
        var summary = await _summarizer.SummarizeAsync(workbook, sampleSize: 20, ct);
        
        await _cache.SetAsync(cacheKey, summary, TimeSpan.FromHours(24), ct);
        
        return summary;
    }

    private static object FormatResult(FormulaResult result)
    {
        return result.ResultType switch
        {
            FormulaResultType.SingleValue => result.Value ?? string.Empty,
            FormulaResultType.Array => result.Value ?? new List<object>(),
            FormulaResultType.Matrix => result.MatrixValue ?? new List<List<object>>(),
            _ => result.Value ?? string.Empty
        };
    }

    private async Task LogQueryAsync(
        NaturalLanguageQueryCommand request,
        FormulaTranslation translation,
        FormulaResult result,
        CancellationToken ct)
    {
        var logEntry = new
        {
            request.Query,
            request.FilePath,
            Formula = translation.Formula,
            ExecutionTime = result.ExecutionTime.TotalMilliseconds,
            Success = result.Success,
            ResultType = result.ResultType.ToString(),
            Timestamp = DateTime.UtcNow
        };
        
        await _activity.PublishAsync("natural_language_query", logEntry, ct);
    }
}