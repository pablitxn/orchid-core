using FluentValidation;
using WebApi.Controllers;

namespace WebApi.Validation;

public sealed class ValidateMessageRequestValidator : AbstractValidator<ValidateMessageRequest>
{
    public ValidateMessageRequestValidator()
    {
        RuleFor(x => x.MessageContent)
            .NotEmpty()
            .MaximumLength(10000);
    }
}

public sealed class ValidateOperationRequestValidator : AbstractValidator<ValidateOperationRequest>
{
    public ValidateOperationRequestValidator()
    {
        RuleFor(x => x.RequiredCredits)
            .GreaterThan(0);

        RuleFor(x => x.OperationType)
            .NotEmpty()
            .MaximumLength(100);
    }
}
