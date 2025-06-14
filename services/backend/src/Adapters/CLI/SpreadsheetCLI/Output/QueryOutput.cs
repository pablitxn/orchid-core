using System.Collections.Generic;

namespace SpreadsheetCLI.Output;

public class QueryOutput
{
    public bool Success { get; set; }
    public string? Answer { get; set; }
    public string? Query { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}