using Domain.Enums;
using FluentValidation;
using WebApi.Controllers;

namespace WebApi.Validation;

public sealed class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
{
    public CreateSubscriptionRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Credits)
            .GreaterThan(0);
    }
}

public sealed class ConsumeCreditsRequestValidator : AbstractValidator<ConsumeCreditsRequest>
{
    public ConsumeCreditsRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0);
    }
}

public sealed class AddCreditsRequestValidator : AbstractValidator<AddCreditsRequest>
{
    public AddCreditsRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0);
    }
}

public sealed class PurchasePlanRequestValidator : AbstractValidator<PurchasePlanRequest>
{
    public PurchasePlanRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Plan)
            .NotEmpty()
            .Must(BeValidPlan)
            .WithMessage("Invalid plan type.");
    }

    private static bool BeValidPlan(string? plan)
        => !string.IsNullOrWhiteSpace(plan)
           && Enum.TryParse<SubscriptionPlanEnum>(plan, true, out _);
}

public sealed class UpdateAutoRenewRequestValidator : AbstractValidator<UpdateAutoRenewRequest>
{
    public UpdateAutoRenewRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
