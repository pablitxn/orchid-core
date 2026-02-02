namespace WebApi.Settings;

/// <summary>
/// Configuration settings for rate limiting on authentication endpoints.
/// </summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Whether rate limiting is enabled. Set to false to disable all rate limiting.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Login endpoint rate limiting settings.
    /// </summary>
    public LoginRateLimitSettings Login { get; set; } = new();

    /// <summary>
    /// Registration endpoint rate limiting settings.
    /// </summary>
    public RegisterRateLimitSettings Register { get; set; } = new();

    /// <summary>
    /// Forgot password endpoint rate limiting settings.
    /// </summary>
    public ForgotPasswordRateLimitSettings ForgotPassword { get; set; } = new();

    /// <summary>
    /// Service token endpoint rate limiting settings.
    /// </summary>
    public ServiceTokenRateLimitSettings ServiceToken { get; set; } = new();
}

public sealed class LoginRateLimitSettings
{
    /// <summary>
    /// Maximum login attempts per IP address within the window.
    /// </summary>
    public int IpLimit { get; set; } = 10;

    /// <summary>
    /// Time window in minutes for IP-based rate limiting.
    /// </summary>
    public int IpWindowMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum login attempts per account within the window.
    /// </summary>
    public int AccountLimit { get; set; } = 5;

    /// <summary>
    /// Time window in minutes for account-based rate limiting.
    /// </summary>
    public int AccountWindowMinutes { get; set; } = 15;
}

public sealed class RegisterRateLimitSettings
{
    /// <summary>
    /// Maximum registration attempts per IP address within the window.
    /// </summary>
    public int Limit { get; set; } = 3;

    /// <summary>
    /// Time window in minutes for registration rate limiting.
    /// </summary>
    public int WindowMinutes { get; set; } = 60;
}

public sealed class ForgotPasswordRateLimitSettings
{
    /// <summary>
    /// Maximum forgot password requests per IP address within the window.
    /// </summary>
    public int Limit { get; set; } = 3;

    /// <summary>
    /// Time window in minutes for forgot password rate limiting.
    /// </summary>
    public int WindowMinutes { get; set; } = 60;
}

public sealed class ServiceTokenRateLimitSettings
{
    /// <summary>
    /// Maximum service token requests per IP address within the window.
    /// </summary>
    public int Limit { get; set; } = 10;

    /// <summary>
    /// Time window in minutes for service token rate limiting.
    /// </summary>
    public int WindowMinutes { get; set; } = 1;
}
