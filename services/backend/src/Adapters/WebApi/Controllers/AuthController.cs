using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Application.Interfaces;
using Application.UseCases.Auth.ForgotPassword;
using Application.UseCases.Auth.Login;
using Application.UseCases.Auth.Register;
using Application.UseCases.Auth.ResetPassword;
using Application.UseCases.User.CreateUser;
using Application.UseCases.User.GetUser;
using Domain.Entities;
using Infrastructure.Telemetry;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IMediator mediator,
    IConfiguration config,
    ITokenService tokenService,
    IWebHostEnvironment env,
    ILogger<AuthController> logger,
    IRateLimitService rateLimitService,
    IPasswordValidator passwordValidator,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtBlacklistService jwtBlacklistService) : ControllerBase
{
    private readonly IConfiguration _config = config;
    private readonly IMediator _mediator = mediator;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IWebHostEnvironment _env = env;
    private readonly ILogger<AuthController> _logger = logger;
    private readonly IRateLimitService _rateLimitService = rateLimitService;
    private readonly IPasswordValidator _passwordValidator = passwordValidator;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly IJwtBlacklistService _jwtBlacklistService = jwtBlacklistService;

    private const int RefreshTokenExpirationDays = 7;

    [HttpGet("mode")]
    [AllowAnonymous]
    public IActionResult Mode()
    {
        var provider = _config["Auth:Provider"] ?? "google";
        return Ok(new { provider });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Rate limiting: 3 registrations per hour per IP
        var rateLimitKey = $"register:{ipAddress}";
        var rateCheck = await _rateLimitService.CheckAsync(
            rateLimitKey,
            limit: 3,
            window: TimeSpan.FromHours(1),
            HttpContext.RequestAborted);

        if (!rateCheck.IsAllowed)
        {
            Response.Headers.Append("Retry-After", ((int)rateCheck.RetryAfter.TotalSeconds).ToString());
            return StatusCode(429, new
            {
                error = "Too many registration attempts. Please try again later.",
                retryAfter = (int)rateCheck.RetryAfter.TotalSeconds
            });
        }

        // Validate email format
        if (string.IsNullOrWhiteSpace(request.Email) ||
            !new EmailAddressAttribute().IsValid(request.Email))
        {
            return BadRequest(new { error = "Valid email address is required" });
        }

        // Validate password using centralized validator
        var passwordValidation = _passwordValidator.Validate(request.Password ?? string.Empty);
        if (!passwordValidation.IsValid)
        {
            return BadRequest(new { error = passwordValidation.ErrorMessage });
        }

        // Security: Only assign default "User" role - admin roles must be assigned by administrators
        var result = await _mediator.Send(new RegisterCommand(
            request.Email,
            request.Password,
            new[] { "User" }));

        if (!result.IsSuccess)
            return Conflict(new { error = result.ErrorMessage });

        // Record Prometheus metrics for successful user registration
        ApplicationMetrics.UserRegistrationsTotal.WithLabels("direct").Inc();

        return Ok(new { result.User!.Id, result.User.Email, message = "Welcome! You've been granted 1000 free credits!" });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var emailNormalized = request.Email?.ToLowerInvariant() ?? "unknown";

        // Rate limiting: Combined IP + account-based limiting for defense in depth
        // IP-based: 10 attempts per 15 minutes (allows for shared IPs)
        var ipRateLimitKey = $"login:ip:{ipAddress}";
        var ipRateCheck = await _rateLimitService.CheckAsync(
            ipRateLimitKey,
            limit: 10,
            window: TimeSpan.FromMinutes(15),
            HttpContext.RequestAborted);

        if (!ipRateCheck.IsAllowed)
        {
            _logger.LogWarning("IP rate limit exceeded for {IpAddress}", ipAddress);
            Response.Headers.Append("Retry-After", ((int)ipRateCheck.RetryAfter.TotalSeconds).ToString());
            return StatusCode(429, new
            {
                error = "Too many login attempts. Please try again later.",
                retryAfter = (int)ipRateCheck.RetryAfter.TotalSeconds
            });
        }

        // Account-based: 5 attempts per 15 minutes per email (stricter per-account limit)
        var accountRateLimitKey = $"login:account:{emailNormalized}";
        var accountRateCheck = await _rateLimitService.CheckAsync(
            accountRateLimitKey,
            limit: 5,
            window: TimeSpan.FromMinutes(15),
            HttpContext.RequestAborted);

        if (!accountRateCheck.IsAllowed)
        {
            _logger.LogWarning("Account rate limit exceeded for email hash (not logging email for privacy)");
            Response.Headers.Append("Retry-After", ((int)accountRateCheck.RetryAfter.TotalSeconds).ToString());
            return StatusCode(429, new
            {
                error = "Too many login attempts for this account. Please try again later.",
                retryAfter = (int)accountRateCheck.RetryAfter.TotalSeconds
            });
        }

        var userAgent = Request.Headers.UserAgent.FirstOrDefault();

        var result = await _mediator.Send(new LoginCommand(
            request.Email,
            request.Password,
            ipAddress,
            userAgent));

        if (!result.IsSuccess)
            return Unauthorized(new { error = result.ErrorMessage });

        // Generate refresh token
        var refreshToken = await GenerateRefreshTokenAsync(result.UserId, ipAddress);

        var expireHours = double.TryParse(_config["Jwt:ExpireHours"], out var hours) ? hours : 4;
        SetTokenCookie(result.Token, expireHours);
        SetRefreshTokenCookie(refreshToken.Token);

        return Ok(new
        {
            token = result.Token,
            refreshToken = refreshToken.Token,
            expiresAt = DateTime.UtcNow.AddHours(expireHours)
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Get refresh token from cookie or request body
        var refreshTokenValue = request?.RefreshToken
            ?? Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(refreshTokenValue))
            return Unauthorized(new { error = "Refresh token is required" });

        var existingToken = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
        if (existingToken == null)
            return Unauthorized(new { error = "Invalid refresh token" });

        if (!existingToken.IsActive)
        {
            // Token reuse detected - revoke all tokens for security
            if (existingToken.IsRevoked)
            {
                _logger.LogWarning("Refresh token reuse detected for user {UserId}", existingToken.UserId);
                await _refreshTokenRepository.RevokeAllUserTokensAsync(
                    existingToken.UserId,
                    "Token reuse detected - potential security breach",
                    ipAddress);
            }
            return Unauthorized(new { error = "Refresh token is no longer valid" });
        }

        // Revoke current token and create new one (rotation)
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.RevokedByIp = ipAddress;
        existingToken.RevokeReason = "Replaced by new token";

        var newRefreshToken = await GenerateRefreshTokenAsync(existingToken.UserId, ipAddress);
        existingToken.ReplacedByToken = newRefreshToken.Token;

        await _refreshTokenRepository.UpdateAsync(existingToken);

        // Generate new access token
        var user = existingToken.User;
        var roles = user.UserRoles?.Select(ur => ur.Role.Name) ?? Array.Empty<string>();
        var newAccessToken = _tokenService.GenerateToken(user, roles);

        var expireHours = double.TryParse(_config["Jwt:ExpireHours"], out var hours) ? hours : 4;
        SetTokenCookie(newAccessToken, expireHours);
        SetRefreshTokenCookie(newRefreshToken.Token);

        return Ok(new
        {
            token = newAccessToken,
            refreshToken = newRefreshToken.Token,
            expiresAt = DateTime.UtcNow.AddHours(expireHours)
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Security: Blacklist the JWT access token to prevent reuse
        var jwtTokenValue = Request.Cookies["token"];
        if (!string.IsNullOrEmpty(jwtTokenValue))
        {
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(jwtTokenValue);
                var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
                var exp = jwtToken.ValidTo;

                if (!string.IsNullOrEmpty(jti) && exp > DateTime.UtcNow)
                {
                    await _jwtBlacklistService.BlacklistTokenAsync(jti, exp);
                    _logger.LogInformation("JWT token blacklisted on logout. JTI: {Jti}, IP: {IpAddress}", jti, ipAddress);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse JWT token for blacklisting during logout");
            }
        }

        // Revoke refresh token if present
        var refreshTokenValue = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshTokenValue))
        {
            var token = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
            if (token != null && token.IsActive)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
                token.RevokeReason = "User logout";
                await _refreshTokenRepository.UpdateAsync(token);
            }
        }

        Response.Cookies.Delete("token");
        Response.Cookies.Delete("refreshToken");
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("revoke-all-tokens")]
    public async Task<IActionResult> RevokeAllTokens()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        await _refreshTokenRepository.RevokeAllUserTokensAsync(userId, "User requested revocation", ipAddress);

        Response.Cookies.Delete("token");
        Response.Cookies.Delete("refreshToken");

        return Ok(new { message = "All tokens revoked successfully" });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Rate limiting: 3 password reset requests per hour per IP
        var rateLimitKey = $"forgot-password:{ipAddress}";
        var rateCheck = await _rateLimitService.CheckAsync(
            rateLimitKey,
            limit: 3,
            window: TimeSpan.FromHours(1),
            HttpContext.RequestAborted);

        if (!rateCheck.IsAllowed)
        {
            Response.Headers.Append("Retry-After", ((int)rateCheck.RetryAfter.TotalSeconds).ToString());
            return StatusCode(429, new
            {
                error = "Too many password reset requests. Please try again later.",
                retryAfter = (int)rateCheck.RetryAfter.TotalSeconds
            });
        }

        // Always process the request, but don't reveal if email exists
        var token = await _mediator.Send(new ForgotPasswordCommand(request.Email));

        // In production, send token via email using IEmailService
        // For now, log it only in development
        if (token != null && _env.IsDevelopment())
        {
            _logger.LogWarning("DEV ONLY - Password reset token generated.");
        }

        // Always return success to prevent email enumeration
        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _mediator.Send(new ResetPasswordCommand(request.Email, request.Token, request.NewPassword));
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Password reset successfully" });
    }


    // Step 1: Redirect user to Google OAuth URL manually
    [HttpGet("google")]
    [AllowAnonymous]
    public IActionResult ManualGoogleLogin()
    {
        var clientId = _config["Authentication:Google:ClientId"]!;
        var configRedirectUri = _config["Authentication:Google:RedirectUri"]
                                ?? $"{Request.Scheme}://{Request.Host}/api/auth/callback/google";
        var redirectUri = HttpUtility.UrlEncode(configRedirectUri);

        var oauthUrl = "https://accounts.google.com/o/oauth2/v2/auth" +
                       "?access_type=online" +
                       $"&client_id={clientId}" +
                       $"&redirect_uri={redirectUri}" +
                       "&response_type=code" +
                       "&scope=email" +
                       "&prompt=consent";

        return Redirect(oauthUrl);
    }

    // Step 2: Handle the OAuth callback manually
    [HttpGet("callback/google")]
    [AllowAnonymous]
    public async Task<IActionResult> ManualGoogleCallback([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Authorization code not provided.");

        // Exchange the authorization code for an access token
        const string tokenEndpoint = "https://oauth2.googleapis.com/token";
        var redirectUri = _config["Authentication:Google:RedirectUri"]
                          ?? $"{Request.Scheme}://{Request.Host}/api/auth/callback/google";
        var clientId = _config["Authentication:Google:ClientId"]!;
        var clientSecret = _config["Authentication:Google:ClientSecret"]!;

        var tokenRequestParams = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", code },
            { "grant_type", "authorization_code" },
            { "redirect_uri", redirectUri },
            { "access_type", "online" }
        };

        OAuthTokenResponse tokenResponse;
        using (var client = new HttpClient())
        {
            var content = new FormUrlEncodedContent(tokenRequestParams);
            var tokenResponseMessage = await client.PostAsync(tokenEndpoint, content);
            var tokenJson = await tokenResponseMessage.Content.ReadAsStringAsync();
            tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(tokenJson)!;
        }

        if (!tokenResponse.IsSuccess)
            return BadRequest("Failed to obtain access token.");

        // Use the access token to retrieve the user's email from Google API
        const string userInfoEndpoint =
            "https://www.googleapis.com/oauth2/v2/userinfo?fields=email,verified_email,name,given_name,family_name,picture,locale,hd";
        string userJson;
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
            var userResponse = await client.GetAsync(userInfoEndpoint);
            userJson = await userResponse.Content.ReadAsStringAsync();
        }

        // Get the user's email from the API response
        var userInfoResponse = JsonSerializer.Deserialize<GoogleUserInfoResponse>(userJson)!;
        if (!userInfoResponse.IsSuccess)
            return BadRequest("Failed to retrieve user email from Google.");

        // Reject if no email provided - don't allow anonymous fallback
        if (string.IsNullOrEmpty(userInfoResponse.Email))
            return BadRequest("Email address not returned by Google. Please ensure your Google account has a verified email.");

        // Get or create user
        var userFromDb = await _mediator.Send(new GetUserQuery(userInfoResponse.Email));

        if (userFromDb == null!)
        {
            await _mediator.Send(new CreateUserCommand(
                userInfoResponse.Email,
                ["User"]
            ));
            userFromDb = await _mediator.Send(new GetUserQuery(userInfoResponse.Email));

            // Record Prometheus metrics for successful Google OAuth registration
            ApplicationMetrics.UserRegistrationsTotal.WithLabels("google").Inc();
        }

        // Generate JWT using the centralized token service (not hardcoded secret)
        var roles = userFromDb?.UserRoles?.Select(ur => ur.Role.Name) ?? ["User"];
        var jwtTokenString = _tokenService.GenerateToken(userFromDb!, roles);

        // Set secure cookie
        var expireHours = double.TryParse(_config["Jwt:ExpireHours"], out var hours) ? hours : 24;
        Response.Cookies.Append("token", jwtTokenString, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(expireHours)
        });

        // Only log in development, and never log the token
        if (_env.IsDevelopment())
        {
            _logger.LogDebug("User authenticated via Google OAuth: {Email}", userInfoResponse.Email);
        }

        return Redirect(_config["FrontendUrl"]!);
    }

    private async Task<RefreshTokenEntity> GenerateRefreshTokenAsync(Guid userId, string? ipAddress)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var tokenString = Convert.ToBase64String(tokenBytes);

        var refreshToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = tokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };

        await _refreshTokenRepository.CreateAsync(refreshToken);
        return refreshToken;
    }

    private void SetTokenCookie(string token, double expireHours)
    {
        Response.Cookies.Append("token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(expireHours)
        });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpirationDays)
        });
    }
}

public record RegisterRequest(string? Name, string? Email, string? Password);

public record LoginRequest(string Email, string Password);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record RefreshTokenRequest(string? RefreshToken);

// Helper class for OAuth token response
public class OAuthTokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
    [JsonPropertyName("email")] public int? ExpiresIn { get; init; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
    [JsonPropertyName("scope")] public string? Scope { get; init; }
    [JsonPropertyName("token_type")] public string? TokenType { get; init; }
    [JsonPropertyName("id_token")] public string? IdToken { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("IsSuccess")] public bool IsSuccess => string.IsNullOrEmpty(Error);

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; }
}

// Helper class for user email API response
public class GoogleUserInfoResponse
{
    [JsonPropertyName("email")] public string? Email { get; set; }

    public ApiError? Error { get; }

    public bool IsSuccess => Error == null;

    public class ApiError
    {
        public required int Code { get; set; }
        public required string Message { get; set; }
        public required string Status { get; set; }
    }
}
