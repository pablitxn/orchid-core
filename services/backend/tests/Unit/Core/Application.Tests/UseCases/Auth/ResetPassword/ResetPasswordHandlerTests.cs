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

    public ResetPasswordHandlerTests()
    {
        _handler = new ResetPasswordHandler(_userRepo.Object, _passwordHasher.Object);
    }

    [Fact]
    public async Task Handle_Resets_WhenTokenValid()
    {
        var user = new UserEntity
            { Email = "x@x.com", PasswordResetToken = "tok", PasswordResetExpiry = DateTime.UtcNow.AddMinutes(5) };
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);
        _passwordHasher.Setup(h => h.HashPassword("newpass")).Returns("hashedpassword");

        var res = await _handler.Handle(new ResetPasswordCommand(user.Email, "tok", "newpass"), CancellationToken.None);

        Assert.True(res);
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
    }
}