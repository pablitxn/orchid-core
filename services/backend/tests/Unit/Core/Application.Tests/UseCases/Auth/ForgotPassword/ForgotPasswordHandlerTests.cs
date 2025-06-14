using Application.Interfaces;
using Application.UseCases.Auth.ForgotPassword;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Auth.ForgotPassword;

public class ForgotPasswordHandlerTests
{
    private readonly ForgotPasswordHandler _handler;
    private readonly Mock<IUserRepository> _userRepo = new();

    public ForgotPasswordHandlerTests()
    {
        _handler = new ForgotPasswordHandler(_userRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsToken_WhenUserExists()
    {
        var user = new UserEntity { Id = Guid.NewGuid(), Email = "test@example.com" };
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);

        var token = await _handler.Handle(new ForgotPasswordCommand(user.Email), CancellationToken.None);

        Assert.NotNull(token);
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
    }
}