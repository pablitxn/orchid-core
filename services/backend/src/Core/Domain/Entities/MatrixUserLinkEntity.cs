namespace Domain.Entities;

/// <summary>
/// Links a Matrix user ID to an Orchid user account.
/// A user can only have one active Matrix link at a time.
/// </summary>
public class MatrixUserLinkEntity
{
    public Guid Id { get; init; }

    /// <summary>
    /// The Orchid user this Matrix account is linked to.
    /// </summary>
    public Guid UserId { get; init; }

    public UserEntity? User { get; private set; }

    /// <summary>
    /// The Matrix user ID in the format @user:matrix.orchid-labs.xyz
    /// </summary>
    public required string MatrixUserId { get; init; }

    /// <summary>
    /// When the link was established.
    /// </summary>
    public DateTime LinkedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this link is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
