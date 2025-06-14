namespace SpreadsheetCLI.Output;

public class CompressionOutput
{
    public bool Success { get; set; }
    public string? OutputFile { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public double CompressionRatio { get; set; }
    public string? Error { get; set; }
    public CompressionStatistics? Statistics { get; set; }
    public int EstimatedTokens { get; set; }
    public string? CompressedContent { get; set; }
    public CompressionMetrics? Metrics { get; set; }
}

public class CompressionStatistics
{
    public double CompressionRatio { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public int RowsProcessed { get; set; }
    public int TablesFound { get; set; }
}

public class CompressionMetrics
{
    public long ProcessingTimeMs { get; set; }
    public decimal TotalCost { get; set; }
}