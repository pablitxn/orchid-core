namespace Domain.Entities.Spreadsheet;

/// <summary>
/// Represents a structured semantic query for workbook data.
/// </summary>
/// <param name="WorksheetName">Target worksheet name.</param>
/// <param name="Goal">User query goal or expression.</param>
public sealed record SemanticQuery(string WorksheetName, string Goal);