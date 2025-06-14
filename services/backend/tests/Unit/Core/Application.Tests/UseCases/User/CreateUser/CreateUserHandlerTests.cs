using Application.Interfaces;
using Application.UseCases.User.CreateUser;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.User.CreateUser;

public class CreateUserHandlerTests
{
    private readonly CreateUserCommandHandler _handler;
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();

    public CreateUserHandlerTests()
    {
        _handler = new CreateUserCommandHandler(_userRepo.Object, _roleRepo.Object);
    }

    [Fact]
    public async Task Handle_CreatesUser_AndAssignsRole()
    {
        var command = new CreateUserCommand("test@example.com", new[] { "admin" });
        _userRepo.Setup(r => r.GetByEmailAsync(command.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync((UserEntity u) => u);
        _roleRepo.Setup(r => r.GetByNameAsync("admin")).ReturnsAsync((RoleEntity?)null);
        _roleRepo.Setup(r => r.CreateAsync(It.IsAny<RoleEntity>())).ReturnsAsync((RoleEntity r) => r);

        var result = await _handler.Handle(command, CancellationToken.None);

        _userRepo.Verify(r => r.CreateAsync(It.IsAny<UserEntity>()), Times.Once);
        _roleRepo.Verify(r => r.AssignRoleToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Once);
        Assert.Equal(command.Email, result.Email);
    }

    [Fact]
    public async Task Handle_ReturnsExistingUser_WhenUserAlreadyExists()
    {
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "exists@example.com",
            Name = "ExistingUser",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var command = new CreateUserCommand(existingUser.Email, new[] { "admin" });
        _userRepo.Setup(r => r.GetByEmailAsync(existingUser.Email)).ReturnsAsync(existingUser);

        var result = await _handler.Handle(command, CancellationToken.None);

        _userRepo.Verify(r => r.CreateAsync(It.IsAny<UserEntity>()), Times.Never);
        _roleRepo.Verify(r => r.CreateAsync(It.IsAny<RoleEntity>()), Times.Never);
        _roleRepo.Verify(r => r.AssignRoleToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        Assert.Equal(existingUser.Email, result.Email);
        Assert.Equal(existingUser.Name, result.Name);
        Assert.Equal(existingUser.Id, result.Id);
        Assert.Equal(existingUser.CreatedAt, result.CreatedAt);
    }

    [Fact]
    public async Task Handle_CreatesUser_AndUsesExistingRole_WhenRoleAlreadyExists()
    {
        var command = new CreateUserCommand("test2@example.com", new[] { "admin" });
        _userRepo.Setup(r => r.GetByEmailAsync(command.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync((UserEntity u) => u);
        var existingRole = new RoleEntity { Id = Guid.NewGuid(), Name = "admin" };
        _roleRepo.Setup(r => r.GetByNameAsync("admin")).ReturnsAsync(existingRole);

        var result = await _handler.Handle(command, CancellationToken.None);

        _roleRepo.Verify(r => r.CreateAsync(It.IsAny<RoleEntity>()), Times.Never);
        _roleRepo.Verify(r => r.AssignRoleToUserAsync(result.Id, existingRole.Id), Times.Once);
        Assert.Equal(command.Email, result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Name));
        Assert.NotEqual(default(DateTime), result.CreatedAt);
    }

    [Fact]
    public async Task Handle_DoesNotAssignRoles_WhenNoRolesProvided()
    {
        var command = new CreateUserCommand("nobody@example.com", null);
        _userRepo.Setup(r => r.GetByEmailAsync(command.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync((UserEntity u) => u);

        var result = await _handler.Handle(command, CancellationToken.None);

        _roleRepo.Verify(r => r.CreateAsync(It.IsAny<RoleEntity>()), Times.Never);
        _roleRepo.Verify(r => r.AssignRoleToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        Assert.Equal(command.Email, result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Name));
    }

    [Fact]
    public async Task Handle_CreatesUser_AndAssignsMultipleRoles_WhenMultipleRolesProvided()
    {
        var roles = new[] { "admin", "user", "moderator" };
        var command = new CreateUserCommand("multi@example.com", roles);
        _userRepo.Setup(r => r.GetByEmailAsync(command.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync((UserEntity u) => u);
        foreach (var roleName in roles)
            _roleRepo.Setup(r => r.GetByNameAsync(roleName)).ReturnsAsync((RoleEntity?)null);
        _roleRepo.Setup(r => r.CreateAsync(It.IsAny<RoleEntity>())).ReturnsAsync((RoleEntity r) => r);

        var result = await _handler.Handle(command, CancellationToken.None);

        _roleRepo.Verify(r => r.CreateAsync(It.Is<RoleEntity>(r => roles.Contains(r.Name))),
            Times.Exactly(roles.Length));
        _roleRepo.Verify(r => r.AssignRoleToUserAsync(result.Id, It.IsAny<Guid>()), Times.Exactly(roles.Length));
        Assert.Equal(command.Email, result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Name));
    }
}