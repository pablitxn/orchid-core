using Application.Interfaces.Spreadsheet;
using Domain.ValueObjects.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;

namespace Application.UseCases.Spreadsheet.Compression;

/// <summary>
/// Factory for creating spreadsheet compression pipelines with fluent configuration.
/// </summary>
public static class SpreadsheetCompressionFactory
{
    /// <summary>
    /// Creates a new pipeline builder with the given service provider.
    /// </summary>
    public static ISpreadsheetPipelineBuilder Create(IServiceProvider serviceProvider)
    {
        return new SpreadsheetPipelineBuilder(serviceProvider);
    }
    
    /// <summary>
    /// Creates a pre-configured pipeline optimized for small spreadsheets (< 10k cells).
    /// Uses inverted index for maximum compression.
    /// </summary>
    public static ISpreadsheetCompressionPipeline CreateLightweightPipeline(IServiceProvider serviceProvider)
    {
        return Create(serviceProvider)
            .AddInvertedIndex(new InvertedIndexOptions
            {
                OptimizeRanges = true,
                IncludeFormats = false,
                RangeThreshold = 2
            })
            .Build();
    }
    
    /// <summary>
    /// Creates a pre-configured pipeline optimized for medium spreadsheets (10k - 100k cells).
    /// Uses format aggregation for better compression on structured data.
    /// </summary>
    public static ISpreadsheetCompressionPipeline CreateStandardPipeline(IServiceProvider serviceProvider)
    {
        return Create(serviceProvider)
            .AddFormatAggregation(new FormatAggregationOptions
            {
                EnableTypeRecognition = true,
                MinGroupSize = 3
            })
            .Build();
    }
    
    /// <summary>
    /// Creates a pre-configured pipeline optimized for large spreadsheets (> 100k cells).
    /// Uses aggressive compression with all available techniques.
    /// </summary>
    public static ISpreadsheetCompressionPipeline CreateHighCompressionPipeline(IServiceProvider serviceProvider)
    {
        return Create(serviceProvider)
            .AddFormatAggregation(new FormatAggregationOptions
            {
                EnableTypeRecognition = true,
                MinGroupSize = 2
            })
            .AddInvertedIndex(new InvertedIndexOptions
            {
                OptimizeRanges = true,
                IncludeFormats = true,
                RangeThreshold = 2
            })
            .Build();
    }
    
    // Pipeline selection thresholds
    private const double SparseDataThreshold = 70.0; // Percentage of empty cells
    private const int SmallDatasetThreshold = 10000; // Total cells
    private const int MediumDatasetThreshold = 100000; // Total cells
    
    /// <summary>
    /// Selects the appropriate pipeline based on workbook characteristics.
    /// </summary>
    public static ISpreadsheetCompressionPipeline CreateOptimalPipeline(
        IServiceProvider serviceProvider, 
        WorkbookContext workbook)
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));
        
        if (workbook?.Statistics == null)
            throw new ArgumentNullException(nameof(workbook), "Workbook or its statistics cannot be null");
        
        var totalCells = workbook.Statistics.TotalCells;
        var emptyPercentage = workbook.Statistics.EmptyPercentage;
        
        // For very sparse data, use inverted index
        if (emptyPercentage > SparseDataThreshold)
        {
            return CreateLightweightPipeline(serviceProvider);
        }
        
        // For small datasets, use lightweight compression
        if (totalCells < SmallDatasetThreshold)
        {
            return CreateLightweightPipeline(serviceProvider);
        }
        
        // For medium datasets, use standard pipeline
        if (totalCells < MediumDatasetThreshold)
        {
            return CreateStandardPipeline(serviceProvider);
        }
        
        // For large datasets, use aggressive compression
        return CreateHighCompressionPipeline(serviceProvider);
    }
}