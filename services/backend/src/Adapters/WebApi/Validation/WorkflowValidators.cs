using Application.UseCases.Workflow.CreateWorkflow;
using FluentValidation;

namespace WebApi.Validation;

public sealed class CreateWorkflowCommandValidator : AbstractValidator<CreateWorkflowCommand>
{
    public CreateWorkflowCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description is not null);

        RuleFor(x => x.PriceCredits)
            .GreaterThanOrEqualTo(0);
    }
}
