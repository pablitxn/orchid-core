namespace Application.UseCases.Social.GetUserSocialIdentities;

/// <summary>
/// DTO representing a linked social identity.
/// </summary>
public record SocialIdentityDto(
    Guid Id,
    string Provider,
    string? Email,
    string? DisplayName,
    string? AvatarUrl,
    bool IsPrimary,
    DateTime LinkedAt,
    DateTime LastUsedAt);
