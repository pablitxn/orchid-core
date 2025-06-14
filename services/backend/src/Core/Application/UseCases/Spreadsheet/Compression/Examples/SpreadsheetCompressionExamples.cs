using Application.Interfaces.Spreadsheet;
using Domain.ValueObjects.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Spreadsheet.Compression.Examples;

/// <summary>
/// Examples demonstrating how to use the spreadsheet compression system.
/// </summary>
public static class SpreadsheetCompressionExamples
{
    /// <summary>
    /// Example 1: Basic inverted index compression for sparse data.
    /// </summary>
    public static async Task<CompressionResult> BasicInvertedIndexExample(
        IServiceProvider serviceProvider, 
        WorkbookContext workbook)
    {
        // Create a lightweight pipeline optimized for sparse data
        var pipeline = SpreadsheetCompressionFactory.CreateLightweightPipeline(serviceProvider);
        
        // Execute compression
        var result = await pipeline.ExecuteAsync(workbook);
        
        Console.WriteLine($"Compression achieved: {result.CompressionRatio:F2}x");
        Console.WriteLine($"Compressed text length: {result.CompressedText.Length} characters");
        
        return result;
    }
    
    /// <summary>
    /// Example 2: Format-aware aggregation for structured financial data.
    /// </summary>
    public static async Task<CompressionResult> FormatAwareAggregationExample(
        IServiceProvider serviceProvider, 
        WorkbookContext workbook)
    {
        // Create a standard pipeline with format recognition
        var pipeline = SpreadsheetCompressionFactory.CreateStandardPipeline(serviceProvider);
        
        // Execute compression
        var result = await pipeline.ExecuteAsync(workbook);
        
        Console.WriteLine($"Format aggregation compression: {result.CompressionRatio:F2}x");
        foreach (var artifact in result.Artifacts)
        {
            Console.WriteLine($"Generated artifact: {artifact.Name} ({artifact.Data.Length} bytes)");
        }
        
        return result;
    }
    
    /// <summary>
    /// Example 3: Custom pipeline with specific configuration.
    /// </summary>
    public static async Task<CompressionResult> CustomPipelineExample(
        IServiceProvider serviceProvider, 
        WorkbookContext workbook)
    {
        // Build custom pipeline
        var pipeline = SpreadsheetCompressionFactory.Create(serviceProvider)
            .AddFormatAggregation(new FormatAggregationOptions
            {
                EnableTypeRecognition = true,
                MinGroupSize = 2
            })
            .AddInvertedIndex(new InvertedIndexOptions
            {
                OptimizeRanges = true,
                IncludeFormats = false,
                RangeThreshold = 3
            })
            .Build();
        
        // Execute with timing
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await pipeline.ExecuteAsync(workbook);
        stopwatch.Stop();
        
        Console.WriteLine($"Custom pipeline executed in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Steps: {string.Join(" → ", result.StepTimings.Keys)}");
        
        return result;
    }
    
    /// <summary>
    /// Example 4: Automatic pipeline selection based on data characteristics.
    /// </summary>
    public static async Task<CompressionResult> AutomaticPipelineSelectionExample(
        IServiceProvider serviceProvider, 
        WorkbookContext workbook)
    {
        // Let the factory choose the optimal pipeline
        var pipeline = SpreadsheetCompressionFactory.CreateOptimalPipeline(serviceProvider, workbook);
        
        // Execute compression
        var result = await pipeline.ExecuteAsync(workbook);
        
        Console.WriteLine($"Auto-selected pipeline for {workbook.Statistics.TotalCells} cells");
        Console.WriteLine($"Empty cells: {workbook.Statistics.EmptyPercentage:F1}%");
        Console.WriteLine($"Achieved compression: {result.CompressionRatio:F2}x");
        
        return result;
    }
    
    /// <summary>
    /// Example 5: Using compression for LLM token optimization.
    /// </summary>
    public static async Task<string> LlmTokenOptimizationExample(
        IServiceProvider serviceProvider,
        WorkbookContext workbook,
        int maxTokens = 4000,
        CancellationToken cancellationToken = default)
    {
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("SpreadsheetCompressionExamples");
        
        // Try different compression strategies until we fit within token limit
        var strategies = new[]
        {
            (name: "Lightweight", factory: (Func<ISpreadsheetCompressionPipeline>)(() => 
                SpreadsheetCompressionFactory.CreateLightweightPipeline(serviceProvider))),
            (name: "Standard", factory: (Func<ISpreadsheetCompressionPipeline>)(() => 
                SpreadsheetCompressionFactory.CreateStandardPipeline(serviceProvider))),
            (name: "High Compression", factory: (Func<ISpreadsheetCompressionPipeline>)(() => 
                SpreadsheetCompressionFactory.CreateHighCompressionPipeline(serviceProvider)))
        };
        
        foreach (var (name, factory) in strategies)
        {
            var pipeline = factory();
            var result = await pipeline.ExecuteAsync(workbook, cancellationToken);
            
            logger?.LogInformation(
                "Strategy {Strategy}: {Tokens} tokens (ratio: {Ratio:F2}x)",
                name, result.CompressedTokenCount, result.CompressionRatio);
            
            if (result.CompressedTokenCount <= maxTokens)
            {
                Console.WriteLine($"✓ Selected {name} strategy: {result.CompressedTokenCount} tokens");
                return result.CompressedText;
            }
        }
        
        // If all strategies exceed token limit, use the most aggressive one
        var fallbackPipeline = SpreadsheetCompressionFactory.CreateHighCompressionPipeline(serviceProvider);
        var fallbackResult = await fallbackPipeline.ExecuteAsync(workbook, cancellationToken);
        
        Console.WriteLine($"⚠ Using fallback strategy: {fallbackResult.CompressedTokenCount} tokens (exceeds limit)");
        return fallbackResult.CompressedText;
    }
    
    /// <summary>
    /// Example 6: Batch processing multiple worksheets.
    /// </summary>
    public static async Task<Dictionary<string, CompressionResult>> BatchProcessingExample(
        IServiceProvider serviceProvider,
        IEnumerable<WorkbookContext> workbooks)
    {
        var results = new Dictionary<string, CompressionResult>();
        var tasks = new List<Task<(string name, CompressionResult result)>>();
        
        foreach (var workbook in workbooks)
        {
            var task = ProcessWorkbookAsync(serviceProvider, workbook);
            tasks.Add(task);
        }
        
        var completedTasks = await Task.WhenAll(tasks);
        
        foreach (var (name, result) in completedTasks)
        {
            results[name] = result;
            Console.WriteLine($"{name}: {result.CompressionRatio:F2}x compression");
        }
        
        return results;
    }
    
    private static async Task<(string name, CompressionResult result)> ProcessWorkbookAsync(
        IServiceProvider serviceProvider,
        WorkbookContext workbook)
    {
        var pipeline = SpreadsheetCompressionFactory.CreateOptimalPipeline(serviceProvider, workbook);
        var result = await pipeline.ExecuteAsync(workbook);
        return (workbook.Name, result);
    }
}