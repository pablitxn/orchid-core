using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Auth.ResetPassword;

public class ResetPasswordHandler(IUserRepository users, IPasswordHasher passwordHasher)
    : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IUserRepository _users = users;

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email);
        if (user == null)
            return false;
        if (user.PasswordResetToken != request.Token || user.PasswordResetExpiry < DateTime.UtcNow)
            return false;
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;
        await _users.UpdateAsync(user);
        return true;
    }
}