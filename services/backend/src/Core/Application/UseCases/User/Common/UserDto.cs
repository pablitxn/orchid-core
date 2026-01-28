using Domain.Entities;

namespace Application.UseCases.User.Common;

public sealed record UserDto
{
    public Guid Id { get; init; }
    public string? Email { get; init; }
    public string? Name { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
    public List<string> Roles { get; init; } = new();

    // Auth0 fields
    public string? Auth0UserId { get; init; }
    public string? Auth0Connection { get; init; }
    public DateTime? Auth0LastSyncedAt { get; init; }

    public static UserDto FromEntity(UserEntity entity) => new()
    {
        Id = entity.Id,
        Email = entity.Email,
        Name = entity.Name,
        AvatarUrl = entity.AvatarUrl,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        DeletedAt = entity.DeletedAt,
        Roles = entity.UserRoles.Select(ur => ur.Role.Name).ToList(),
        Auth0UserId = entity.Auth0UserId,
        Auth0Connection = entity.Auth0Connection,
        Auth0LastSyncedAt = entity.Auth0LastSyncedAt
    };
}
