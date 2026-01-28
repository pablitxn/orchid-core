using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.UseCases.Auth.SyncAuth0User;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller for handling Auth0 Post-Login Action webhooks.
/// No JWT authorization - uses webhook secret for verification.
/// </summary>
[ApiController]
[Route("api/webhooks/auth0")]
public class Auth0WebhookController(
    IMediator mediator,
    IConfiguration configuration,
    ILogger<Auth0WebhookController> logger) : ControllerBase
{
    /// <summary>
    /// Receives and processes Auth0 Post-Login Action webhook.
    /// </summary>
    [HttpPost("post-login")]
    [AllowAnonymous]
    public async Task<IActionResult> HandlePostLogin(CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        // Validate webhook signature
        var signature = Request.Headers["X-Auth0-Signature"].ToString();
        var webhookSecret = configuration["Auth0:WebhookSecret"];

        if (!string.IsNullOrEmpty(webhookSecret) && !ValidateSignature(payload, signature, webhookSecret))
        {
            logger.LogWarning("Auth0 webhook received with invalid signature from {IpAddress}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized("Invalid webhook signature");
        }

        try
        {
            var webhookData = JsonSerializer.Deserialize<Auth0PostLoginWebhook>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhookData?.User == null)
            {
                logger.LogWarning("Auth0 webhook received with invalid payload");
                return BadRequest("Invalid webhook payload");
            }

            // Extract roles from permissions or roles claim
            var roles = webhookData.User.Permissions?.ToList()
                ?? webhookData.User.Roles?.ToList()
                ?? new List<string>();

            // Extract connection from identities
            var connection = webhookData.User.Identities?.FirstOrDefault()?.Connection;

            // Extract all identities for social login tracking
            var identities = webhookData.User.Identities?
                .Where(i => !string.IsNullOrEmpty(i.Provider) && !string.IsNullOrEmpty(i.UserId))
                .Select(i => new Auth0IdentityDto(
                    i.Provider!,
                    i.UserId!,
                    webhookData.User.Email,
                    webhookData.User.Name))
                .ToList() ?? new List<Auth0IdentityDto>();

            // Extract MFA status from Auth0
            Auth0MfaDto? mfa = null;
            if (webhookData.User.Multifactor != null && webhookData.User.Multifactor.Length > 0)
            {
                // User has MFA enrolled - take first method as primary
                mfa = new Auth0MfaDto(
                    Enabled: true,
                    Method: webhookData.User.Multifactor.FirstOrDefault());
            }
            else if (webhookData.User.MultifactorAuthenticated == true)
            {
                // MFA was used but we don't have method details
                mfa = new Auth0MfaDto(Enabled: true, Method: null);
            }

            var result = await mediator.Send(new SyncAuth0UserCommand(
                webhookData.User.UserId,
                webhookData.User.Email,
                webhookData.User.Name,
                webhookData.User.Picture,
                connection,
                roles,
                identities,
                mfa), cancellationToken);

            logger.LogInformation(
                "Auth0 post-login webhook processed. UserId: {UserId}, IsNew: {IsNew}, Linked: {Linked}",
                result.UserId, result.IsNewUser, result.WasLinked);

            return Ok(new
            {
                userId = result.UserId,
                isNewUser = result.IsNewUser,
                wasLinked = result.WasLinked
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process Auth0 webhook");
            return StatusCode(500, "Failed to process webhook");
        }
    }

    private static bool ValidateSignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedSignature = Convert.ToHexString(hash).ToLowerInvariant();
        var providedSignature = signature.Replace("sha256=", "").ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(providedSignature));
    }
}

/// <summary>
/// DTO for Auth0 Post-Login Action webhook payload.
/// </summary>
public record Auth0PostLoginWebhook
{
    public Auth0WebhookUser? User { get; init; }
    public Auth0WebhookConnection? Connection { get; init; }
}

/// <summary>
/// User data from Auth0 webhook payload.
/// </summary>
public record Auth0WebhookUser
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Picture { get; init; }
    public string[]? Permissions { get; init; }
    public string[]? Roles { get; init; }
    public Auth0Identity[]? Identities { get; init; }

    /// <summary>
    /// MFA methods enrolled by the user (e.g., "totp", "sms", "push-notification").
    /// </summary>
    public string[]? Multifactor { get; init; }

    /// <summary>
    /// Whether MFA was used in this authentication.
    /// </summary>
    public bool? MultifactorAuthenticated { get; init; }
}

/// <summary>
/// Auth0 identity information.
/// </summary>
public record Auth0Identity
{
    public string? Connection { get; init; }
    public string? Provider { get; init; }
    public string? UserId { get; init; }
}

/// <summary>
/// Auth0 connection information.
/// </summary>
public record Auth0WebhookConnection
{
    public string? Name { get; init; }
    public string? Strategy { get; init; }
}
