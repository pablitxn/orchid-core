using Core.Application.DTOs.KnowledgeBase;
using FluentValidation;

namespace WebApi.Validation;

public sealed class UpdateKnowledgeBaseDtoValidator : AbstractValidator<UpdateKnowledgeBaseDto>
{
    public UpdateKnowledgeBaseDtoValidator()
    {
        RuleFor(x => x)
            .Must(HasAnyUpdate)
            .WithMessage("At least one field must be provided.");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .When(x => x.Title is not null);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description is not null);

        RuleForEach(x => x.Tags)
            .NotEmpty()
            .MaximumLength(50)
            .When(x => x.Tags is not null);
    }

    private static bool HasAnyUpdate(UpdateKnowledgeBaseDto dto)
    {
        return dto.Title is not null || dto.Description is not null || (dto.Tags is { Count: > 0 });
    }
}
