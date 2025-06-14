using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Auth.ForgotPassword;

public class ForgotPasswordHandler(IUserRepository users) : IRequestHandler<ForgotPasswordCommand, string?>
{
    private readonly IUserRepository _users = users;

    public async Task<string?> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email);
        if (user == null)
            return null;
        var token = Guid.NewGuid().ToString("N");
        user.PasswordResetToken = token;
        user.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);
        await _users.UpdateAsync(user);
        return token;
    }
}