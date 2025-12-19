namespace Application.Interfaces;

public interface IPasswordValidator
{
    PasswordValidationResult Validate(string password);
}

public record PasswordValidationResult(bool IsValid, string? ErrorMessage = null)
{
    public static PasswordValidationResult Success() => new(true);
    public static PasswordValidationResult Failure(string message) => new(false, message);
}
