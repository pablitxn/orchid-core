using System.ComponentModel.DataAnnotations;
using Application.UseCases.Subscription.AddCredits;
using Application.UseCases.Subscription.ConsumeCredits;
using Application.UseCases.Subscription.CreateSubscription;
using Application.UseCases.Subscription.GetSubscription;
using Application.UseCases.Subscription.PurchasePlan;
using Application.UseCases.Subscription.UpdateAutoRenew;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

// Ensure only authenticated users can access subscription endpoints
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest request)
    {
        try
        {
            var cmd = new CreateSubscriptionCommand(request.UserId, request.Credits, request.ExpiresAt);
            var result = await _mediator.Send(cmd);
            return Ok(new { result.Id, result.UserId, result.Credits, result.ExpiresAt });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while creating the subscription.");
        }
    }

    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] ConsumeCreditsRequest request)
    {
        // Validate request model
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Check current subscription and credits
        var current = await _mediator.Send(new GetSubscriptionQuery(request.UserId));
        if (current == null)
            return NotFound();
        if (current.Credits < request.Amount)
            return BadRequest("Insufficient credits available.");

        var cmd = new ConsumeCreditsCommand(request.UserId, request.Amount);
        var result = await _mediator.Send(cmd);
        return Ok(new { result.UserId, result.Credits });
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] AddCreditsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var cmd = new AddCreditsCommand(request.UserId, request.Amount);
        var result = await _mediator.Send(cmd);
        return Ok(new { result.UserId, result.Credits });
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchasePlanRequest request)
    {
        if (!Enum.TryParse<SubscriptionPlanEnum>(request.Plan, true, out var plan))
            return BadRequest("Invalid plan type.");

        var cmd = new PurchasePlanCommand(request.UserId, plan);
        var result = await _mediator.Send(cmd);
        return Ok(new { result.UserId, result.Credits, result.ExpiresAt });
    }

    [HttpPost("auto-renew")]
    public async Task<IActionResult> ToggleAutoRenew([FromBody] UpdateAutoRenewRequest request)
    {
        var cmd = new UpdateAutoRenewCommand(request.UserId, request.AutoRenew);
        var result = await _mediator.Send(cmd);
        return Ok(new { result.UserId, result.AutoRenew });
    }
    
    [HttpPut("auto-renew")]
    public async Task<IActionResult> UpdateAutoRenew([FromBody] UpdateAutoRenewRequest request)
    {
        var cmd = new UpdateAutoRenewCommand(request.UserId, request.AutoRenew);
        var result = await _mediator.Send(cmd);
        return Ok(new { result.UserId, result.AutoRenew });
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> Get(Guid userId)
    {
        var result = await _mediator.Send(new GetSubscriptionQuery(userId));
        if (result == null) return NotFound();
        return Ok(new { result.UserId, result.Credits, result.ExpiresAt, result.AutoRenew });
    }
    
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }
        
        var subscription = await _mediator.Send(new GetSubscriptionQuery(userId));
        if (subscription == null) return NotFound();
        
        return Ok(new 
        { 
            subscription.UserId, 
            subscription.Credits, 
            subscription.ExpiresAt, 
            subscription.AutoRenew,
            subscription.SubscriptionPlanId,
            IsActive = subscription.ExpiresAt == null || subscription.ExpiresAt > DateTime.UtcNow
        });
    }
    
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }
        
        // For now, return empty history - this would need a new use case
        return Ok(new { history = new object[] { } });
    }
}

public record CreateSubscriptionRequest(
    [Required] Guid UserId,
    [Range(1, int.MaxValue)] int Credits,
    DateTime? ExpiresAt);

public record ConsumeCreditsRequest(
    [Required] Guid UserId,
    [Range(1, int.MaxValue)] int Amount);

public record AddCreditsRequest(
    [Required] Guid UserId,
    [Range(1, int.MaxValue)] int Amount);

public record PurchasePlanRequest(
    [Required] Guid UserId,
    [Required] string Plan);

public record UpdateAutoRenewRequest(
    [Required] Guid UserId,
    bool AutoRenew);