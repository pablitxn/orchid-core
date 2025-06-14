using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Application.UseCases.Auth.ForgotPassword;
using Application.UseCases.Auth.Login;
using Application.UseCases.Auth.Register;
using Application.UseCases.Auth.ResetPassword;
using Application.UseCases.User.CreateUser;
using Application.UseCases.User.GetUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IMediator mediator, IConfiguration config) : ControllerBase
{
    private readonly IConfiguration _config = config;
    private readonly IMediator _mediator = mediator;

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
        // Simplified registration - accept any email without validation for testing
        // Default role is User
        var roles = request.Roles?.Any() == true ? request.Roles : new[] { "User" };

        var user = await _mediator.Send(new RegisterCommand(
            request.Email ?? $"test_{Guid.NewGuid():N}@playground.com", 
            request.Password ?? "defaultPassword123!", 
            roles));
        
        return Ok(new { user.Id, user.Email, message = "Welcome! You've been granted 1000 free credits!" });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password));
        if (!result.IsSuccess)
            return Unauthorized(result.ErrorMessage);
        Response.Cookies.Append("token", result.Token, new CookieOptions { HttpOnly = true });
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
        var token = await _mediator.Send(new ForgotPasswordCommand(request.Email));
        if (token == null)
            return NotFound();
        // For demo purposes we return the token in the response
        return Ok(new { token });
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
        // These values should be securely stored in configuration
        // get from secrets
        var clientId = _config["Authentication:Google:ClientId"]!;
        var redirectUri = HttpUtility.UrlEncode("http://localhost:5210/api/auth/callback/google");
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
        const string redirectUri = "http://localhost:5210/api/auth/callback/google";
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
            // Set the Authorization header with the access token
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
            var userResponse = await client.GetAsync(userInfoEndpoint);
            userJson = await userResponse.Content.ReadAsStringAsync();
        }

        // Get the user's email from the API response
        var userInfoResponse = JsonSerializer.Deserialize<GoogleUserInfoResponse>(userJson)!;
        if (!userInfoResponse.IsSuccess)
            return BadRequest("Failed to retrieve user email from Google.");
        if (string.IsNullOrEmpty(userInfoResponse.Email))
            userInfoResponse.Email = "anonymous";
        // return BadRequest("Email address not returned by Google.");

        // Create JWT token using the user's email claim
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, userInfoResponse.Email!)
            // Include additional claims as needed
        };


        // Configure JWT token (replace with your configuration)
        const string jwtSecret = "your_super_secure_long_secret_key_123!";
        var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var jwtCred = new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha256);

        var jwtToken = new JwtSecurityToken(
            "tuIssuer",
            "tuAudience",
            claims,
            expires: DateTime.UtcNow.AddHours(72),
            signingCredentials: jwtCred
        );

        var jwtTokenString = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        // check if the user exists
        var userInfoFromDb = await _mediator.Send(new GetUserCommand(userInfoResponse.Email!));

        if (userInfoFromDb == null!)
            await _mediator.Send(new CreateUserCommand(
                userInfoResponse.Email!,
                ["User"] // Default role for new users
            ));

        // Set the token as an HTTP-only cookie
        Response.Cookies.Append("token", jwtTokenString, new CookieOptions { HttpOnly = true });

        // Log user email and token (development only)
        Console.WriteLine("User (manual): " + userInfoResponse.Email);
        Console.WriteLine("JWT Token (manual): " + jwtTokenString);

        // Redirect to frontend with email as a query parameter
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