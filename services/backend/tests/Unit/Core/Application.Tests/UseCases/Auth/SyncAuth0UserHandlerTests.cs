using Application.Interfaces;
using Application.UseCases.Auth.SyncAuth0User;
using Domain.Entities;
using Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.Auth;

public class SyncAuth0UserHandlerTests
{
    private readonly SyncAuth0UserHandler _handler;
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<ISocialIdentityRepository> _socialIdentityRepo = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<ILogger<SyncAuth0UserHandler>> _logger = new();

    public SyncAuth0UserHandlerTests()
    {
        _socialIdentityRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserSocialIdentityEntity>());

        _handler = new SyncAuth0UserHandler(
            _userRepo.Object,
            _roleRepo.Object,
            _socialIdentityRepo.Object,
            _eventPublisher.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_CreatesNewUser_WhenNoExistingUserFound()
    {
        // Arrange
        var command = new SyncAuth0UserCommand(
            Auth0UserId: "auth0|123456",
            Email: "new@example.com",
            Name: "New User",
            AvatarUrl: "https://example.com/avatar.png",
            Connection: "google-oauth2",
            Roles: new List<string>(),
            Identities: Array.Empty<Auth0IdentityDto>());

        _userRepo.Setup(r => r.GetByAuth0UserIdAsync(command.Auth0UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.GetByEmailAsync(command.Email))
            .ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>()))
            .ReturnsAsync((UserEntity u) => u);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsNewUser);
        Assert.False(result.WasLinked);
        Assert.False(result.ProfileUpdated);
        _userRepo.Verify(r => r.CreateAsync(It.Is<UserEntity>(u =>
            u.Auth0UserId == command.Auth0UserId &&
            u.Email == command.Email.ToLowerInvariant() &&
            u.Name == command.Name &&
            u.AvatarUrl == command.AvatarUrl &&
            u.Auth0Connection == command.Connection)), Times.Once);
        _eventPublisher.Verify(e => e.PublishAsync(
            It.Is<UserCreatedFromAuth0Event>(ev =>
                ev.Auth0UserId == command.Auth0UserId &&
                ev.Email == command.Email.ToLowerInvariant()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LinksExistingUser_WhenEmailMatches()
    {
        // Arrange
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            Name = "Existing User",
            UserRoles = new List<UserRoleEntity>()
        };

        var command = new SyncAuth0UserCommand(
            Auth0UserId: "auth0|654321",
            Email: "existing@example.com",
            Name: "Updated Name",
            AvatarUrl: null,
            Connection: "auth0",
            Roles: new List<string>(),
            Identities: Array.Empty<Auth0IdentityDto>());

        _userRepo.Setup(r => r.GetByAuth0UserIdAsync(command.Auth0UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.GetByEmailAsync(command.Email.ToLowerInvariant()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsNewUser);
        Assert.True(result.WasLinked);
        Assert.Equal(existingUser.Id, result.UserId);
        Assert.Equal(command.Auth0UserId, existingUser.Auth0UserId);
        Assert.Equal(command.Connection, existingUser.Auth0Connection);
        _userRepo.Verify(r => r.UpdateAsync(existingUser), Times.Once);
        _eventPublisher.Verify(e => e.PublishAsync(
            It.Is<Auth0UserLinkedEvent>(ev =>
                ev.UserId == existingUser.Id &&
                ev.Auth0UserId == command.Auth0UserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdatesProfile_WhenNameOrAvatarChanged()
    {
        // Arrange
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "profile@example.com",
            Name = "Old Name",
            AvatarUrl = "https://old.avatar.com/pic.png",
            Auth0UserId = "auth0|profile123",
            UserRoles = new List<UserRoleEntity>()
        };

        var command = new SyncAuth0UserCommand(
            Auth0UserId: "auth0|profile123",
            Email: "profile@example.com",
            Name: "New Name",
            AvatarUrl: "https://new.avatar.com/pic.png",
            Connection: "auth0",
            Roles: new List<string>(),
            Identities: Array.Empty<Auth0IdentityDto>());

        _userRepo.Setup(r => r.GetByAuth0UserIdAsync(command.Auth0UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsNewUser);
        Assert.False(result.WasLinked);
        Assert.True(result.ProfileUpdated);
        Assert.Equal("New Name", existingUser.Name);
        Assert.Equal("https://new.avatar.com/pic.png", existingUser.AvatarUrl);
        _userRepo.Verify(r => r.UpdateAsync(existingUser), Times.Once);
        _eventPublisher.Verify(e => e.PublishAsync(
            It.Is<UserProfileSyncedEvent>(ev =>
                ev.UserId == existingUser.Id &&
                ev.Name == "New Name" &&
                ev.AvatarUrl == "https://new.avatar.com/pic.png"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SyncsRoles_WhenRolesProvided()
    {
        // Arrange
        var existingRole = new RoleEntity { Id = Guid.NewGuid(), Name = "user" };
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "roles@example.com",
            Name = "User",
            Auth0UserId = "auth0|roles123",
            UserRoles = new List<UserRoleEntity>()
        };

        var command = new SyncAuth0UserCommand(
            Auth0UserId: "auth0|roles123",
            Email: "roles@example.com",
            Name: "User",
            AvatarUrl: null,
            Connection: "auth0",
            Roles: new List<string> { "user", "admin" },
            Identities: Array.Empty<Auth0IdentityDto>());

        _userRepo.Setup(r => r.GetByAuth0UserIdAsync(command.Auth0UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _roleRepo.Setup(r => r.GetRolesForUserAsync(existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoleEntity>());
        _roleRepo.Setup(r => r.GetByNameAsync("user")).ReturnsAsync(existingRole);
        _roleRepo.Setup(r => r.GetByNameAsync("admin")).ReturnsAsync((RoleEntity?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.RolesUpdated);
        _roleRepo.Verify(r => r.AssignRoleToUserAsync(existingUser.Id, existingRole.Id), Times.Once);
        _eventPublisher.Verify(e => e.PublishAsync(
            It.Is<UserRolesUpdatedEvent>(ev => ev.UserId == existingUser.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotUpdate_WhenAlreadyLinkedAndNoChanges()
    {
        // Arrange
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "nochange@example.com",
            Name = "Same Name",
            AvatarUrl = "https://same.avatar.com/pic.png",
            Auth0UserId = "auth0|nochange123",
            UserRoles = new List<UserRoleEntity>()
        };

        var command = new SyncAuth0UserCommand(
            Auth0UserId: "auth0|nochange123",
            Email: "nochange@example.com",
            Name: "Same Name",
            AvatarUrl: "https://same.avatar.com/pic.png",
            Connection: "auth0",
            Roles: new List<string>(),
            Identities: Array.Empty<Auth0IdentityDto>());

        _userRepo.Setup(r => r.GetByAuth0UserIdAsync(command.Auth0UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsNewUser);
        Assert.False(result.WasLinked);
        Assert.False(result.ProfileUpdated);
        Assert.False(result.RolesUpdated);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<UserEntity>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NormalizesEmail_ToLowerCase()
    {
        // Arrange
        var command = new SyncAuth0UserCommand(
            Auth0UserId: "auth0|case123",
            Email: "UPPERCASE@EXAMPLE.COM",
            Name: "User",
            AvatarUrl: null,
            Connection: "auth0",
            Roles: new List<string>(),
            Identities: Array.Empty<Auth0IdentityDto>());

        _userRepo.Setup(r => r.GetByAuth0UserIdAsync(command.Auth0UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.GetByEmailAsync("uppercase@example.com"))
            .ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<UserEntity>()))
            .ReturnsAsync((UserEntity u) => u);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepo.Verify(r => r.GetByEmailAsync("uppercase@example.com"), Times.Once);
        _userRepo.Verify(r => r.CreateAsync(It.Is<UserEntity>(u =>
            u.Email == "uppercase@example.com")), Times.Once);
    }

    [Fact]
    public async Task Handle_RemovesRoles_NotInAuth0()
    {
        // Arrange
        var oldRole = new RoleEntity { Id = Guid.NewGuid(), Name = "old-role" };
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "remove-role@example.com",
            Name = "User",
            Auth0UserId = "auth0|removerole123",
            UserRoles = new List<UserRoleEntity>()
        };

        var command = new SyncAuth0UserCommand(
            Auth0UserId: "auth0|removerole123",
            Email: "remove-role@example.com",
            Name: "User",
            AvatarUrl: null,
            Connection: "auth0",
            Roles: new List<string> { "new-role" },
            Identities: Array.Empty<Auth0IdentityDto>()); // old-role should be removed

        _userRepo.Setup(r => r.GetByAuth0UserIdAsync(command.Auth0UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _roleRepo.Setup(r => r.GetRolesForUserAsync(existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoleEntity> { oldRole });
        _roleRepo.Setup(r => r.GetByNameAsync("new-role")).ReturnsAsync((RoleEntity?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.RolesUpdated);
        _roleRepo.Verify(r => r.RemoveRoleFromUserAsync(existingUser.Id, oldRole.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_SyncsSocialIdentities_WhenProvided()
    {
        // Arrange
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "social@example.com",
            Name = "Social User",
            Auth0UserId = "google-oauth2|123456",
            UserRoles = new List<UserRoleEntity>()
        };

        var identities = new List<Auth0IdentityDto>
        {
            new("google-oauth2", "123456", "social@example.com", "Social User")
        };

        var command = new SyncAuth0UserCommand(
            Auth0UserId: "google-oauth2|123456",
            Email: "social@example.com",
            Name: "Social User",
            AvatarUrl: null,
            Connection: "google-oauth2",
            Roles: new List<string>(),
            Identities: identities);

        _userRepo.Setup(r => r.GetByAuth0UserIdAsync(command.Auth0UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _socialIdentityRepo.Setup(r => r.GetByUserIdAsync(existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserSocialIdentityEntity>());
        _socialIdentityRepo.Setup(r => r.CreateAsync(It.IsAny<UserSocialIdentityEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSocialIdentityEntity e, CancellationToken _) => e);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _socialIdentityRepo.Verify(r => r.CreateAsync(
            It.Is<UserSocialIdentityEntity>(i =>
                i.UserId == existingUser.Id &&
                i.Provider == "google-oauth2" &&
                i.ProviderUserId == "123456"),
            It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(e => e.PublishAsync(
            It.Is<SocialIdentityLinkedEvent>(ev =>
                ev.UserId == existingUser.Id &&
                ev.Provider == "google-oauth2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
