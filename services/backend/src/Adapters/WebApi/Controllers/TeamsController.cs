using Application.UseCases.Team.CreateTeam;
using Application.UseCases.Team.ListTeams;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetTeams()
    {
        var teams = await _mediator.Send(new ListTeamsQuery());
        return Ok(teams);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequest request)
    {
        var command = request.ToCommand();
        var team = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTeams), new { id = team.Id }, team);
    }
}