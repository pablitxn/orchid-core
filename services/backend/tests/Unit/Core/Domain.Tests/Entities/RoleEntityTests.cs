using Domain.Entities;

namespace Domain.Tests.Entities;

public class RoleEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        var role = new RoleEntity
        {
            Id = id,
            Name = "Admin"
        };

        // Assert
        Assert.Equal(id, role.Id);
        Assert.Equal("Admin", role.Name);
    }

    [Fact]
    public void UserRoles_DefaultsToEmptyList()
    {
        // Arrange
        var role = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = "User"
        };

        // Assert
        Assert.Empty(role.UserRoles);
    }

    [Fact]
    public void UserRoles_CanBeModified()
    {
        // Arrange
        var role = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = "Admin"
        };
        var userRole = new UserRoleEntity
        {
            UserId = Guid.NewGuid(),
            RoleId = role.Id
        };

        // Act
        role.UserRoles.Add(userRole);

        // Assert
        Assert.Single(role.UserRoles);
        Assert.Equal(userRole, role.UserRoles[0]);
    }
}
