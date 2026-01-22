using Domain.Entities;

namespace Domain.Tests.Entities;

public class UserRoleEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var userRole = new UserRoleEntity
        {
            UserId = userId,
            RoleId = roleId
        };

        // Assert
        Assert.Equal(userId, userRole.UserId);
        Assert.Equal(roleId, userRole.RoleId);
    }

    [Fact]
    public void NavigationProperties_CanBeSet()
    {
        // Arrange
        var user = new UserEntity { Id = Guid.NewGuid(), Email = "test@example.com" };
        var role = new RoleEntity { Id = Guid.NewGuid(), Name = "Admin" };

        var userRole = new UserRoleEntity
        {
            UserId = user.Id,
            User = user,
            RoleId = role.Id,
            Role = role
        };

        // Assert
        Assert.Equal(user, userRole.User);
        Assert.Equal(role, userRole.Role);
    }
}
