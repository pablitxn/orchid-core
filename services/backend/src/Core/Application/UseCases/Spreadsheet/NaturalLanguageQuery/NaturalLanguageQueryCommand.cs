using MediatR;

namespace Application.UseCases.Spreadsheet.NaturalLanguageQuery;

/// <summary>
/// Command for executing natural language queries against spreadsheets.
/// </summary>
public sealed record NaturalLanguageQueryCommand(
    string FilePath,
    string Query,
    string? WorksheetName = null,
    string? UserId = null) : IRequest<NaturalLanguageQueryResponse>;