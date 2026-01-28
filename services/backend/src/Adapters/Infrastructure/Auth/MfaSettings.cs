namespace Orchid.Infrastructure.Auth;

/// <summary>
/// Configuration settings for MFA enforcement.
/// </summary>
public sealed class MfaSettings
{
    /// <summary>
    /// Configuration section name in appsettings.
    /// </summary>
    public const string SectionName = "Mfa";

    /// <summary>
    /// Whether MFA enforcement is enabled.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// List of roles that require MFA to be enabled.
    /// Users with these roles will be blocked if they don't have MFA set up.
    /// </summary>
    public string[] RequiredForRoles { get; set; } = ["admin", "super_admin"];
}
