using Application.UseCases.PersonalityTemplate.CreateTemplate;
using Application.UseCases.PersonalityTemplate.DeleteTemplate;
using Application.UseCases.PersonalityTemplate.GetTemplate;
using Application.UseCases.PersonalityTemplate.ListTemplates;
using Application.UseCases.PersonalityTemplate.UpdateTemplate;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/personality-templates")]
[Authorize]
public class PersonalityTemplatesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListTemplates([FromQuery] bool includeInactive = false)
    {
        var query = new ListTemplatesQuery(includeInactive);
        var templates = await mediator.Send(query);
        return Ok(templates);
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTemplate(Guid id)
    {
        var query = new GetTemplateQuery(id);
        var template = await mediator.Send(query);
        
        if (template == null)
            return NotFound();
            
        return Ok(template);
    }
    
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateCommand command)
    {
        var template = await mediator.Send(command);
        return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
    }
    
    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateCommand command)
    {
        if (id != command.Id)
            return BadRequest("ID mismatch");
            
        var template = await mediator.Send(command);
        
        if (template == null)
            return NotFound();
            
        return Ok(template);
    }
    
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var command = new DeleteTemplateCommand(id);
        var result = await mediator.Send(command);
        
        if (!result)
            return NotFound();
            
        return NoContent();
    }
}