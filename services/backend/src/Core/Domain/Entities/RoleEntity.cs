namespace Domain.Entities;

public class RoleEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public List<UserRoleEntity> UserRoles { get; set; } = new();
}