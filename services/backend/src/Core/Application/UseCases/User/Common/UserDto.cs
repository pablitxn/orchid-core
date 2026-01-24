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

    public static UserDto FromEntity(UserEntity entity) => new()
    {
        Id = entity.Id,
        Email = entity.Email,
        Name = entity.Name,
        AvatarUrl = entity.AvatarUrl,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        DeletedAt = entity.DeletedAt,
        Roles = entity.UserRoles.Select(ur => ur.Role.Name).ToList()
    };
}
