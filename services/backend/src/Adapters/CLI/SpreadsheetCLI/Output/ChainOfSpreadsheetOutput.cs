namespace SpreadsheetCLI.Output;

public class ChainOfSpreadsheetOutput
{
    public bool Success { get; set; }
    public string? Answer { get; set; }
    public string? DetectedTable { get; set; }
    public string? Error { get; set; }
    public OutputMetrics? Metrics { get; set; }
    public OutputTrace? Trace { get; set; }
}

public class OutputMetrics
{
    public long ProcessingTimeMs { get; set; }
    public decimal TotalCost { get; set; }
}

public class OutputTrace
{
    public string? TableDetection { get; set; }
    public string? QuestionAnswering { get; set; }
    public string? TotalDuration { get; set; }
    public decimal TotalCost { get; set; }
}