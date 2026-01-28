using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.Auth.GetLegacyUser;
using Application.UseCases.Auth.ValidateLegacyCredentials;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Endpoints for Auth0 Custom Database Connection scripts.
/// These endpoints are called by Auth0, not by users directly.
/// </summary>
[ApiController]
[Route("api/auth0-migration")]
public class Auth0MigrationController(
    IMediator mediator,
    IConfiguration configuration,
    IRateLimitService rateLimitService,
    ILogger<Auth0MigrationController> logger) : ControllerBase
{
    private const int RateLimitPerMinute = 30;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Called by Auth0 Custom DB login script to validate legacy credentials.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateLogin(CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        if (!ValidateSignature(payload))
        {
            logger.LogWarning(
                "Auth0 migration login: Invalid signature from {IpAddress}",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "Invalid signature" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateLimitKey = $"auth0-migration:login:{ipAddress}";
        var rateCheck = await rateLimitService.CheckAsync(
            rateLimitKey,
            RateLimitPerMinute,
            RateLimitWindow,
            cancellationToken);

        if (!rateCheck.IsAllowed)
        {
            logger.LogWarning("Auth0 migration login: Rate limit exceeded for {IpAddress}", ipAddress);
            return StatusCode(429, new { error = "Too many requests" });
        }

        try
        {
            var request = JsonSerializer.Deserialize<Auth0MigrationLoginRequest>(
                payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { error = "Email and password required" });
            }

            var result = await mediator.Send(
                new ValidateLegacyCredentialsCommand(request.Email, request.Password),
                cancellationToken);

            if (!result.IsValid)
            {
                return Unauthorized(new { error = "Invalid credentials" });
            }

            return Ok(new
            {
                user_id = result.UserId.ToString(),
                email = result.Email,
                name = result.Name,
                picture = result.AvatarUrl,
                email_verified = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auth0 migration login: Unexpected error");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Called by Auth0 Custom DB get_user script to check if user exists.
    /// </summary>
    [HttpPost("get-user")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUser(CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        if (!ValidateSignature(payload))
        {
            logger.LogWarning(
                "Auth0 migration get-user: Invalid signature from {IpAddress}",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "Invalid signature" });
        }

        try
        {
            var request = JsonSerializer.Deserialize<Auth0MigrationGetUserRequest>(
                payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrEmpty(request.Email))
            {
                return BadRequest(new { error = "Email required" });
            }

            var result = await mediator.Send(
                new GetLegacyUserQuery(request.Email),
                cancellationToken);

            if (result == null)
            {
                return NotFound(new { error = "User not found" });
            }

            if (result.IsMigrated)
            {
                return NotFound(new { error = "User already migrated" });
            }

            return Ok(new
            {
                user_id = result.UserId.ToString(),
                email = result.Email,
                name = result.Name,
                picture = result.AvatarUrl,
                email_verified = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auth0 migration get-user: Unexpected error");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private bool ValidateSignature(string payload)
    {
        var signature = Request.Headers["X-Auth0-Migration-Signature"].ToString();
        var secret = configuration["Auth0:MigrationSecret"];

        if (string.IsNullOrEmpty(secret))
        {
            logger.LogWarning("Auth0 migration: No MigrationSecret configured");
            return false;
        }

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

public record Auth0MigrationLoginRequest(string Email, string Password);
public record Auth0MigrationGetUserRequest(string Email);
