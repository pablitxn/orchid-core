// using Application.UseCases.Spreadsheet.NaturalLanguageQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SpreadsheetController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Execute a natural language query against a spreadsheet.
    /// </summary>
    /// <param name="request">The natural language query request</param>
    /// <returns>Query result with formula and explanation</returns>
    [HttpPost("natural-language-query")]
    // [ProducesResponseType(typeof(NaturalLanguageQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> NaturalLanguageQuery([FromBody] NaturalLanguageQueryRequest request)
    {
        // if (string.IsNullOrWhiteSpace(request.Query))
        // {
        //     return BadRequest("Query cannot be empty");
        // }
        //
        // if (string.IsNullOrWhiteSpace(request.FilePath))
        // {
        //     return BadRequest("File path cannot be empty");
        // }
        //
        // var command = new NaturalLanguageQueryCommand(
        //     request.FilePath,
        //     request.Query,
        //     request.WorksheetName,
        //     User.Identity?.Name);
        //
        // var response = await _mediator.Send(command);

        return Ok(/*response*/);
    }
}

public class NaturalLanguageQueryRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string? WorksheetName { get; set; }
}