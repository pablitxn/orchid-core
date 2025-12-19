using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Auth.ResetPassword;

public class ResetPasswordHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IPasswordValidator passwordValidator,
    IRefreshTokenRepository refreshTokenRepository)
    : IRequestHandler<ResetPasswordCommand, ResetPasswordResult>
{
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IPasswordValidator _passwordValidator = passwordValidator;
    private readonly IUserRepository _users = users;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

    public async Task<ResetPasswordResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Validate password complexity before any database operations
        var validationResult = _passwordValidator.Validate(request.NewPassword);
        if (!validationResult.IsValid)
            return ResetPasswordResult.Failure(validationResult.ErrorMessage!);

        // Security: Normalize email for consistent lookup
        var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        var user = await _users.GetByEmailAsync(normalizedEmail);
        if (user == null)
            return ResetPasswordResult.Failure("Invalid reset request");

        // Security: Check expiry first (before comparing token) to prevent timing attacks
        if (user.PasswordResetExpiry < DateTime.UtcNow)
            return ResetPasswordResult.Failure("Invalid or expired reset token");

        // Security: Use constant-time comparison to prevent timing attacks
        if (string.IsNullOrEmpty(user.PasswordResetToken) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(user.PasswordResetToken),
                Encoding.UTF8.GetBytes(request.Token)))
            return ResetPasswordResult.Failure("Invalid or expired reset token");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;
        user.PasswordChangedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        // Revoke all existing refresh tokens for security
        await _refreshTokenRepository.RevokeAllUserTokensAsync(
            user.Id,
            "Password reset - all sessions invalidated",
            ct: cancellationToken);

        return ResetPasswordResult.Success();
    }
}

public record ResetPasswordResult(bool IsSuccess, string? ErrorMessage = null)
{
    public static ResetPasswordResult Success() => new(true);
    public static ResetPasswordResult Failure(string message) => new(false, message);
}