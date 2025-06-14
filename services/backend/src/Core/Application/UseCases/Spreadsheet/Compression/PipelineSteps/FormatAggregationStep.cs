using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Linq;
using Application.Interfaces.Spreadsheet;
using Domain.ValueObjects.Spreadsheet;

namespace Application.UseCases.Spreadsheet.Compression.PipelineSteps;

public sealed class FormatAggregationStep : IPipelineStep
{
    private readonly IFormatAwareAggregator _aggregator;
    private readonly FormatAggregationOptions? _options;
    
    public string Name => "FormatAggregation";
    
    public FormatAggregationStep(IFormatAwareAggregator aggregator, FormatAggregationOptions? options = null)
    {
        _aggregator = aggregator;
        _options = options;
    }
    
    public async Task<PipelineStepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var outputs = new Dictionary<string, object>();
            var compressedParts = new List<string>();
            var totalCompressionRatio = 0.0;
            
            // Get the workbook to process
            var workbook = context.IntermediateResults.ContainsKey("SkeletonWorkbook")
                ? (WorkbookContext)context.IntermediateResults["SkeletonWorkbook"]
                : context.OriginalWorkbook;
            
            foreach (var worksheet in workbook.Worksheets)
            {
                var aggregated = await _aggregator.AggregateAsync(worksheet, _options, cancellationToken);
                
                // Convert aggregated regions to ultra-compact representation
                var compactLines = new List<string>();
                foreach (var region in aggregated.Regions)
                {
                    if (region.CellCount > 1)
                    {
                        // Multi-cell region: TYPE:START-END:FORMAT
                        var format = string.IsNullOrEmpty(region.FormatString) ? "" : $":{region.FormatString}";
                        compactLines.Add($"{region.TypeToken}:{region.StartAddress}-{region.EndAddress}{format}");
                    }
                    else
                    {
                        // Single cell: maintain original value for accuracy
                        var cellData = worksheet.Cells.First(c => c.Address.Equals(region.StartAddress));
                        var value = cellData.GetStringValue();
                        compactLines.Add($"{region.StartAddress}={value}");
                    }
                }
                
                var worksheetCompact = string.Join("\n", compactLines);
                compressedParts.Add($"## {worksheet.Name}\n{worksheetCompact}");
                
                outputs[$"Worksheet_{worksheet.Name}_Aggregated"] = worksheetCompact;
                outputs[$"Worksheet_{worksheet.Name}_CompressionRatio"] = aggregated.CompressionRatio;
                
                totalCompressionRatio += aggregated.CompressionRatio;
            }
            
            var compressedText = string.Join("\n\n", compressedParts);
            outputs["CompressedText"] = compressedText;
            
            // Calculate token count
            var tokenCount = Encoding.UTF8.GetByteCount(compressedText);
            outputs["TokenCount"] = tokenCount;
            
            // Average compression ratio
            outputs["AverageCompressionRatio"] = totalCompressionRatio / workbook.Worksheets.Count;
            
            // Store artifact
            context.Artifacts.Add(new PipelineArtifact
            {
                Name = "format_aggregated.json",
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