using Application.Interfaces;
using Application.UseCases.Auth.Login;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Auth.Login;

public class LoginHandlerTests
{
    private readonly LoginHandler _handler;
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();

    public LoginHandlerTests()
    {
        _handler = new LoginHandler(_userRepo.Object, _tokenService.Object, _passwordHasher.Object);
    }

    [Fact]
    public async Task Handle_ReturnsToken_WhenUserExists_AndPasswordMatches()
    {
        var email = "foo@bar.com";
        var password = "secret";
        _userRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new UserEntity { 
                Id = Guid.NewGuid(), 
                Email = email, 
                PasswordHash = password,
                UserRoles = new List<Domain.Entities.UserRoleEntity>()
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
    }

    [Fact]
    public async Task Handle_Fails_WhenPasswordIncorrect()
    {
        var email = "foo@bar.com";
        _userRepo.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new UserEntity { 
                Id = Guid.NewGuid(), 
                Email = email, 
                PasswordHash = "correct",
                UserRoles = new List<Domain.Entities.UserRoleEntity>()
            });
        _passwordHasher.Setup(h => h.VerifyPassword("correct", "wrong"))
            .Returns(false);

        var result = await _handler.Handle(new LoginCommand(email, "wrong"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid password", result.ErrorMessage);
    }
}