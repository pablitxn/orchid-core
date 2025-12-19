using Application.Interfaces;
using Application.UseCases.Auth.ResetPassword;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Auth.ResetPassword;

public class ResetPasswordHandlerTests
{
    private readonly ResetPasswordHandler _handler;
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IPasswordValidator> _passwordValidator = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepo = new();

    public ResetPasswordHandlerTests()
    {
        _passwordValidator.Setup(v => v.Validate(It.IsAny<string>()))
            .Returns(PasswordValidationResult.Success());

        _handler = new ResetPasswordHandler(
            _userRepo.Object,
            _passwordHasher.Object,
            _passwordValidator.Object,
            _refreshTokenRepo.Object);
    }

    [Fact]
    public async Task Handle_Resets_WhenTokenValid()
    {
        var user = new UserEntity
            { Id = Guid.NewGuid(), Email = "x@x.com", PasswordResetToken = "tok", PasswordResetExpiry = DateTime.UtcNow.AddMinutes(5) };
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);
        _passwordHasher.Setup(h => h.HashPassword("NewPass1!")).Returns("hashedpassword");

        var res = await _handler.Handle(new ResetPasswordCommand(user.Email, "tok", "NewPass1!"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
        _refreshTokenRepo.Verify(r => r.RevokeAllUserTokensAsync(
            user.Id,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Fails_WhenPasswordInvalid()
    {
        _passwordValidator.Setup(v => v.Validate("weak"))
            .Returns(PasswordValidationResult.Failure("Password too weak"));

        var res = await _handler.Handle(new ResetPasswordCommand("x@x.com", "tok", "weak"), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Equal("Password too weak", res.ErrorMessage);
    }
}