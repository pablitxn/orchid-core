using Application.Interfaces;
using Application.UseCases.Auth.Login;
using Core.Application.Interfaces;
using Core.Domain.Entities;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.Auth;

public class LoginHandlerTests
{
    private readonly LoginHandler _handler;
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ILoginHistoryRepository> _loginHistoryRepo = new();
    private readonly Mock<ILogger<LoginHandler>> _logger = new();

    public LoginHandlerTests()
    {
        _loginHistoryRepo.Setup(r => r.GetFailedAttemptsCountAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _loginHistoryRepo.Setup(r => r.CreateAsync(
                It.IsAny<LoginHistoryEntity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginHistoryEntity e, CancellationToken _) => e);

        _handler = new LoginHandler(
            _userRepo.Object,
            _tokenService.Object,
            _passwordHasher.Object,
            _loginHistoryRepo.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsToken_WhenUserExists_AndPasswordMatches()
    {
        var email = "foo@bar.com";
        var password = "secret";
        _userRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = password,
                UserRoles = new List<UserRoleEntity>()
            });
        _tokenService.Setup(s => s.GenerateToken(It.IsAny<UserEntity>(), It.IsAny<IEnumerable<string>>()))
            .Returns("token123");
        _passwordHasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), password))
            .Returns(true);

        var result = await _handler.Handle(new LoginCommand(email, password), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("token123", result.Token);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserMissing()
    {
        var email = "x@x.com";
        _userRepo.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync((UserEntity?)null);

        var result = await _handler.Handle(new LoginCommand(email, "any"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid credentials", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_Fails_WhenPasswordIncorrect()
    {
        var email = "foo@bar.com";
        var userId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new UserEntity
            {
                Id = userId,
                Email = email,
                PasswordHash = "correct",
                UserRoles = new List<UserRoleEntity>()
            });
        _passwordHasher.Setup(h => h.VerifyPassword("correct", "wrong"))
            .Returns(false);

        var result = await _handler.Handle(new LoginCommand(email, "wrong"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid credentials", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_Fails_WhenAccountLocked()
    {
        var email = "locked@bar.com";
        var userId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new UserEntity
            {
                Id = userId,
                Email = email,
                PasswordHash = "password",
                UserRoles = new List<UserRoleEntity>()
            });

        // Simulate 5 failed attempts (account should be locked)
        _loginHistoryRepo.Setup(r => r.GetFailedAttemptsCountAsync(
                userId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _handler.Handle(new LoginCommand(email, "password"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Account temporarily locked. Please try again later.", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_RecordsLoginAttempt_OnSuccess()
    {
        var email = "foo@bar.com";
        var password = "secret";
        var userId = Guid.NewGuid();

        _userRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new UserEntity
            {
                Id = userId,
                Email = email,
                PasswordHash = password,
                UserRoles = new List<UserRoleEntity>()
            });
        _tokenService.Setup(s => s.GenerateToken(It.IsAny<UserEntity>(), It.IsAny<IEnumerable<string>>()))
            .Returns("token123");
        _passwordHasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), password))
            .Returns(true);

        await _handler.Handle(new LoginCommand(email, password, "127.0.0.1", "TestAgent"), CancellationToken.None);

        _loginHistoryRepo.Verify(r => r.CreateAsync(
            It.Is<LoginHistoryEntity>(h =>
                h.UserId == userId &&
                h.Success == true &&
                h.IpAddress == "127.0.0.1" &&
                h.UserAgent == "TestAgent"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RecordsLoginAttempt_OnFailure()
    {
        var email = "foo@bar.com";
        var userId = Guid.NewGuid();

        _userRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new UserEntity
            {
                Id = userId,
                Email = email,
                PasswordHash = "correct",
                UserRoles = new List<UserRoleEntity>()
            });
        _passwordHasher.Setup(h => h.VerifyPassword("correct", "wrong"))
            .Returns(false);

        await _handler.Handle(new LoginCommand(email, "wrong", "127.0.0.1", null), CancellationToken.None);

        _loginHistoryRepo.Verify(r => r.CreateAsync(
            It.Is<LoginHistoryEntity>(h =>
                h.UserId == userId &&
                h.Success == false &&
                h.FailureReason == "Invalid password"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_PassesRememberFlagToTokenService(bool rememberMe)
    {
        var email = "foo@bar.com";
        var password = "secret";
        _userRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = password,
                UserRoles = new List<UserRoleEntity>()
            });
        _tokenService.Setup(s => s.GenerateToken(It.IsAny<UserEntity>(), It.IsAny<IEnumerable<string>>(), rememberMe))
            .Returns("token123");
        _passwordHasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), password))
            .Returns(true);

        var result = await _handler.Handle(new LoginCommand(email, password, null, null, rememberMe), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("token123", result.Token);
        _tokenService.Verify(s => s.GenerateToken(It.IsAny<UserEntity>(), It.IsAny<IEnumerable<string>>(), rememberMe), Times.Once);
    }
}
