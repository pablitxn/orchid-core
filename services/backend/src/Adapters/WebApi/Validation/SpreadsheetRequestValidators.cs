using FluentValidation;
using WebApi.Controllers;

namespace WebApi.Validation;

public sealed class NaturalLanguageQueryRequestValidator : AbstractValidator<NaturalLanguageQueryRequest>
{
    public NaturalLanguageQueryRequestValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(x => x.Query)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.WorksheetName)
            .MaximumLength(128)
            .When(x => x.WorksheetName is not null);
    }
}
