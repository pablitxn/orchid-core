using System.Security.Cryptography;
using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Auth.ForgotPassword;

public class ForgotPasswordHandler(IUserRepository users) : IRequestHandler<ForgotPasswordCommand, ForgotPasswordDto>
{
    private readonly IUserRepository _users = users;

    public async Task<ForgotPasswordDto> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // Security: Normalize email for consistent lookup
        var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        var user = await _users.GetByEmailAsync(normalizedEmail);
        if (user == null)
            return new ForgotPasswordDto(null);

        // Generate cryptographically secure token (32 bytes = 256 bits)
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        user.PasswordResetToken = token;
        user.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);
        await _users.UpdateAsync(user);
        return new ForgotPasswordDto(token);
    }
}