namespace Application.UseCases.User.GetMfaStatus;

/// <summary>
/// DTO representing the MFA status for a user.
/// </summary>
public record MfaStatusDto(
    bool Enabled,
    string? Method,
    DateTime? EnrolledAt,
    DateTime? LastVerifiedAt);
