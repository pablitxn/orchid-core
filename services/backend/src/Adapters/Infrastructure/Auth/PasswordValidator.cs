using System.Text.RegularExpressions;
using Application.Interfaces;

namespace Infrastructure.Auth;

public partial class PasswordValidator : IPasswordValidator
{
    private const int MinLength = 8;

    public PasswordValidationResult Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return PasswordValidationResult.Failure("Password is required");

        if (password.Length < MinLength)
            return PasswordValidationResult.Failure($"Password must be at least {MinLength} characters");

        if (!PasswordComplexityRegex().IsMatch(password))
            return PasswordValidationResult.Failure(
                "Password must contain uppercase, lowercase, digit, and special character");

        return PasswordValidationResult.Success();
    }

    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#\-_])[A-Za-z\d@$!%*?&#\-_]{8,}$")]
    private static partial Regex PasswordComplexityRegex();
}
