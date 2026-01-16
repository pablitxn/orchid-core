using FluentValidation;
using Application.Interfaces;
using WebApi.Controllers;

namespace WebApi.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(IPasswordValidator passwordValidator)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        RuleFor(x => x.Name)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.Password)
            .NotEmpty()
            .Custom((password, context) =>
            {
                var result = passwordValidator.Validate(password ?? string.Empty);
                if (!result.IsValid)
                {
                    context.AddFailure(result.ErrorMessage ?? "Password does not meet security requirements.");
                }
            });
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator(IPasswordValidator passwordValidator)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .Custom((password, context) =>
            {
                var result = passwordValidator.Validate(password ?? string.Empty);
                if (!result.IsValid)
                {
                    context.AddFailure(result.ErrorMessage ?? "Password does not meet security requirements.");
                }
            });
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        When(x => x.RefreshToken != null, () =>
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty()
                .MaximumLength(512);
        });
    }
}
