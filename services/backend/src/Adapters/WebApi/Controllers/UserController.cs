using System.Security.Claims;
using Application.UseCases.User.GetUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetUser()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        var result = await _mediator.Send(new GetUserCommand(email));

        if (result == null!)
            // todo: fix me
            // mock result
            return Ok();

        return Ok(new
        {
            result.Id,
            result.Email,
            result.Name
        });
    }
}