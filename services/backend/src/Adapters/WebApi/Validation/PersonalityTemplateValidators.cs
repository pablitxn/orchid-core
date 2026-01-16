using Application.UseCases.PersonalityTemplate.CreateTemplate;
using Application.UseCases.PersonalityTemplate.UpdateTemplate;
using FluentValidation;

namespace WebApi.Validation;

public sealed class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.Prompt)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateTemplateCommandValidator : AbstractValidator<UpdateTemplateCommand>
{
    public UpdateTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.Prompt)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0);
    }
}
