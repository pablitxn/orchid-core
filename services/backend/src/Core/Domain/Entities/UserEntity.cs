namespace Domain.Entities;

public class UserEntity
{
    public Guid Id { get; init; }

    public string? Email { get; set; }

    public string? Name { get; set; }

    public string? AvatarUrl { get; set; }

    [Obsolete("Use SubscriptionEntity.Credits instead.")]
    public int? Credits { get; set; }

    [Obsolete("Use SubscriptionEntity to determine subscription activity instead.")]
    public bool SubscriptionActive { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public List<UserRoleEntity> UserRoles { get; set; } = new();

    // Optional hashed password for authentication
    public string PasswordHash { get; set; } = string.Empty;

    // Optional password reset token and expiration
    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetExpiry { get; set; }

    public DateTime? PasswordChangedAt { get; set; }
}