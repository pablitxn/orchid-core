using Application.Interfaces;
using Application.UseCases.Auth.Register;
using Domain.Entities;
using Domain.Events;
using Moq;

namespace Application.Tests.UseCases.Auth;

public class RegisterHandlerTests
{
    private readonly RegisterHandler _handler;
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();

    public RegisterHandlerTests()
    {
        _handler = new RegisterHandler(
            _userRepo.Object,
            _roleRepo.Object,
            _passwordHasher.Object,
            _eventPublisher.Object);
    }

    [Fact]
    public async Task Handle_CreatesUser_WhenNotExists()
    {
        var cmd = new RegisterCommand("foo@bar.com", "pass", new[] { "User" });
        _userRepo.Setup(r => r.GetByEmailAsync(cmd.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync((UserEntity u) => u);
        _roleRepo.Setup(r => r.GetByNameAsync("User"))
            .ReturnsAsync(new RoleEntity { Id = Guid.NewGuid(), Name = "User" });

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepo.Verify(r => r.CreateAsync(It.IsAny<UserEntity>()), Times.Once);
        Assert.Equal(cmd.Email, result.User!.Email);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenUserExists()
    {
        var existing = new UserEntity { Id = Guid.NewGuid(), Email = "foo@bar.com" };
        _userRepo.Setup(r => r.GetByEmailAsync(existing.Email)).ReturnsAsync(existing);

        var result = await _handler.Handle(new RegisterCommand(existing.Email, "pass", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("An account with this email already exists", result.ErrorMessage);
        _userRepo.Verify(r => r.CreateAsync(It.IsAny<UserEntity>()), Times.Never);
        _eventPublisher.Verify(p => p.PublishAsync(It.IsAny<UserCreatedEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UsesExistingRole_WhenRoleExists()
    {
        var cmd = new RegisterCommand("foo@bar.com", "pass", new[] { "Admin" });
        _userRepo.Setup(r => r.GetByEmailAsync(cmd.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync(new UserEntity());
        _roleRepo.Setup(r => r.GetByNameAsync("Admin"))
            .ReturnsAsync(new RoleEntity { Id = Guid.NewGuid(), Name = "Admin" });

        await _handler.Handle(cmd, CancellationToken.None);

        _roleRepo.Verify(r => r.CreateAsync(It.IsAny<RoleEntity>()), Times.Never);
        _roleRepo.Verify(r => r.AssignRoleToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SkipsRole_WhenRoleMissing()
    {
        // Changed behavior: now skips unknown roles instead of creating them
        var cmd = new RegisterCommand("foo@bar.com", "pass", new[] { "UnknownRole" });
        _userRepo.Setup(r => r.GetByEmailAsync(cmd.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>()))
            .ReturnsAsync(new UserEntity { Id = Guid.NewGuid() });
        _roleRepo.Setup(r => r.GetByNameAsync("UnknownRole")).ReturnsAsync((RoleEntity?)null);

        await _handler.Handle(cmd, CancellationToken.None);

        _roleRepo.Verify(r => r.CreateAsync(It.IsAny<RoleEntity>()), Times.Never);
        _roleRepo.Verify(r => r.AssignRoleToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HashesPassword()
    {
        var cmd = new RegisterCommand("foo@bar.com", "pass", null);
        _userRepo.Setup(r => r.GetByEmailAsync(cmd.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync((UserEntity u) => u);
        _passwordHasher.Setup(h => h.HashPassword(cmd.Password)).Returns("hash");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hash", result.User!.PasswordHash);
    }

    [Fact]
    public async Task Handle_PublishesEvent()
    {
        var cmd = new RegisterCommand("foo@bar.com", "pass", null);
        _userRepo.Setup(r => r.GetByEmailAsync(cmd.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>()))
            .ReturnsAsync(new UserEntity { Id = Guid.NewGuid(), Email = cmd.Email });

        await _handler.Handle(cmd, CancellationToken.None);

        _eventPublisher.Verify(p => p.PublishAsync(It.IsAny<UserCreatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AssignsMultipleRoles()
    {
        var roles = new[] { "Admin", "User" };
        var cmd = new RegisterCommand("foo@bar.com", "pass", roles);
        _userRepo.Setup(r => r.GetByEmailAsync(cmd.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>()))
            .ReturnsAsync(new UserEntity { Id = Guid.NewGuid() });
        _roleRepo.Setup(r => r.GetByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(new RoleEntity { Name = "test", Id = Guid.NewGuid() });

        await _handler.Handle(cmd, CancellationToken.None);

        _roleRepo.Verify(r => r.AssignRoleToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Exactly(roles.Length));
    }

    [Fact]
    public async Task Handle_NoRoles_DoesNotQueryRoleRepo()
    {
        var cmd = new RegisterCommand("foo@bar.com", "pass", null);
        _userRepo.Setup(r => r.GetByEmailAsync(cmd.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync(new UserEntity());

        await _handler.Handle(cmd, CancellationToken.None);

        _roleRepo.Verify(r => r.GetByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SetsGeneratedName()
    {
        var cmd = new RegisterCommand("foo@bar.com", "pass", null);
        _userRepo.Setup(r => r.GetByEmailAsync(cmd.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync((UserEntity u) => u);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("user_", result.User!.Name);
    }

    [Fact]
    public async Task Handle_EventContainsUserId()
    {
        var cmd = new RegisterCommand("foo@bar.com", "pass", null);
        var created = new UserEntity { Id = Guid.NewGuid(), Email = cmd.Email };
        _userRepo.Setup(r => r.GetByEmailAsync(cmd.Email)).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>())).ReturnsAsync(created);

        await _handler.Handle(cmd, CancellationToken.None);

        _eventPublisher.Verify(p => p.PublishAsync(It.Is<UserCreatedEvent>(e => e.UserId == created.Id)), Times.Once);
    }
}