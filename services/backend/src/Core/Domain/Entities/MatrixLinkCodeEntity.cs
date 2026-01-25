namespace Domain.Entities;

/// <summary>
/// Temporary verification code for linking a Matrix account to an Orchid user.
/// Codes expire after 5 minutes and can only be used once.
/// </summary>
public class MatrixLinkCodeEntity
{
    /// <summary>
    /// Code expiration time in minutes.
    /// </summary>
    public const int ExpirationMinutes = 5;

    public Guid Id { get; init; }

    /// <summary>
    /// The Orchid user requesting the link.
    /// </summary>
    public Guid UserId { get; init; }

    public UserEntity? User { get; private set; }

    /// <summary>
    /// The verification code in format ORCHID-[A-Z0-9]{4} (e.g., ORCHID-A7X9)
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// When this code expires (5 minutes after creation).
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// When this code was used to complete the link. Null if not yet used.
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// Checks if this code has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Checks if this code has already been used.
    /// </summary>
    public bool IsUsed => UsedAt.HasValue;

    /// <summary>
    /// Checks if this code is valid for use (not expired and not used).
    /// </summary>
    public bool IsValid => !IsExpired && !IsUsed;

    /// <summary>
    /// Marks this code as used.
    /// </summary>
    public void MarkAsUsed()
    {
        UsedAt = DateTime.UtcNow;
    }
}
