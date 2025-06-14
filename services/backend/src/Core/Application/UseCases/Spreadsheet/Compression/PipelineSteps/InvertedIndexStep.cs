using System.Diagnostics;
using System.Text;
using Application.Interfaces.Spreadsheet;
using Domain.ValueObjects.Spreadsheet;

namespace Application.UseCases.Spreadsheet.Compression.PipelineSteps;

public sealed class InvertedIndexStep : IPipelineStep
{
    private readonly IInvertedIndexTranslator _translator;
    private readonly InvertedIndexOptions? _options;
    
    public string Name => "InvertedIndex";
    
    public InvertedIndexStep(IInvertedIndexTranslator translator, InvertedIndexOptions? options = null)
    {
        _translator = translator;
        _options = options;
    }
    
    public async Task<PipelineStepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var outputs = new Dictionary<string, object>();
            var compressedParts = new List<string>();
            
            // Get the workbook to process (could be original or skeleton)
            var workbook = context.IntermediateResults.ContainsKey("SkeletonWorkbook")
                ? (WorkbookContext)context.IntermediateResults["SkeletonWorkbook"]
                : context.OriginalWorkbook;
            
            // Process worksheets in parallel for better performance
            var worksheetTasks = workbook.Worksheets.Select(async worksheet =>
            {
                // Check cancellation before processing each worksheet
                cancellationToken.ThrowIfCancellationRequested();
                
                var indexJson = await _translator.ToInvertedIndexAsync(worksheet, _options);
                return new
                {
                    worksheet.Name,
                    IndexJson = indexJson,
                    Header = $"## {worksheet.Name}\n{indexJson}"
                };
            }).ToList();
            
            var worksheetResults = await Task.WhenAll(worksheetTasks);
            
            foreach (var result in worksheetResults)
            {
                compressedParts.Add(result.Header);
                outputs[$"Worksheet_{result.Name}_Index"] = result.IndexJson;
            }
            
            var compressedText = string.Join("\n\n", compressedParts);
            outputs["CompressedText"] = compressedText;
            
            // Calculate token count (UTF-8 byte count as approximation)
            var tokenCount = Encoding.UTF8.GetByteCount(compressedText);
            outputs["TokenCount"] = tokenCount;
            
            // Store artifact
            context.Artifacts.Add(new PipelineArtifact
            {
                Name = "inverted_index.json",
                Type = "application/json",
                Data = Encoding.UTF8.GetBytes(compressedText)
            });
            
            return new PipelineStepResult
            {
                Success = true,
                Outputs = outputs,
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new PipelineStepResult
            {
                Success = false,
                Error = ex.Message,
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }
}