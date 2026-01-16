using FluentValidation;
using WebApi.Controllers;

namespace WebApi.Validation;

public sealed class UpdateBillingPreferencesRequestValidator : AbstractValidator<UpdateBillingPreferencesRequest>
{
    public UpdateBillingPreferencesRequestValidator()
    {
        RuleFor(x => x.MessageBillingMethod)
            .NotEmpty()
            .Must(method => method is "tokens" or "fixed")
            .WithMessage("Invalid billing method. Must be 'tokens' or 'fixed'.");

        RuleFor(x => x.TokenRate)
            .GreaterThan(0);

        RuleFor(x => x.FixedMessageRate)
            .GreaterThan(0);

        RuleFor(x => x.LowCreditThreshold)
            .GreaterThanOrEqualTo(0)
            .When(x => x.LowCreditThreshold.HasValue);
    }
}
