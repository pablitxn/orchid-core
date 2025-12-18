using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using Application.Interfaces;
using Application.UseCases.Auth.ForgotPassword;
using Application.UseCases.Auth.Login;
using Application.UseCases.Auth.Register;
using Application.UseCases.Auth.ResetPassword;
using Application.UseCases.User.CreateUser;
using Application.UseCases.User.GetUser;
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
    IRateLimitService rateLimitService) : ControllerBase
{
    private readonly IConfiguration _config = config;
    private readonly IMediator _mediator = mediator;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IWebHostEnvironment _env = env;
    private readonly ILogger<AuthController> _logger = logger;
    private readonly IRateLimitService _rateLimitService = rateLimitService;

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

        // Validate password
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new { error = "Password must be at least 8 characters" });
        }

        // Password complexity: at least one uppercase, lowercase, digit, and special char
        var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#\-_])[A-Za-z\d@$!%*?&#\-_]{8,}$");
        if (!passwordRegex.IsMatch(request.Password))
        {
            return BadRequest(new { error = "Password must contain uppercase, lowercase, digit, and special character" });
        }

        var roles = request.Roles?.Any() == true ? request.Roles : new[] { "User" };

        var user = await _mediator.Send(new RegisterCommand(
            request.Email,
            request.Password,
            roles));

        return Ok(new { user.Id, user.Email, message = "Welcome! You've been granted 1000 free credits!" });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Rate limiting: 5 attempts per 15 minutes per IP
        var rateLimitKey = $"login:{ipAddress}";
        var rateCheck = await _rateLimitService.CheckAsync(
            rateLimitKey,
            limit: 5,
            window: TimeSpan.FromMinutes(15),
            HttpContext.RequestAborted);

        if (!rateCheck.IsAllowed)
        {
            Response.Headers.Append("Retry-After", ((int)rateCheck.RetryAfter.TotalSeconds).ToString());
            return StatusCode(429, new
            {
                error = "Too many login attempts. Please try again later.",
                retryAfter = (int)rateCheck.RetryAfter.TotalSeconds
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

        var expireHours = double.TryParse(_config["Jwt:ExpireHours"], out var hours) ? hours : 24;
        Response.Cookies.Append("token", result.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(expireHours)
        });

        return Ok(new { token = result.Token });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("token");
        return Ok();
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
            _logger.LogWarning("DEV ONLY - Password reset token for {Email}: {Token}", request.Email, token);
        }

        // Always return success to prevent email enumeration
        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var success = await _mediator.Send(new ResetPasswordCommand(request.Email, request.Token, request.NewPassword));
        if (!success)
            return BadRequest();
        return Ok();
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
        var userFromDb = await _mediator.Send(new GetUserCommand(userInfoResponse.Email));

        if (userFromDb == null!)
        {
            await _mediator.Send(new CreateUserCommand(
                userInfoResponse.Email,
                ["User"]
            ));
            userFromDb = await _mediator.Send(new GetUserCommand(userInfoResponse.Email));
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
}

public record RegisterRequest(string? Name, string? Email, string? Password, IEnumerable<string>? Roles);

public record LoginRequest(string Email, string Password);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

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