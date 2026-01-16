using Application.UseCases.Plugin.CreatePlugin;
using FluentValidation;

namespace WebApi.Validation;

public sealed class CreatePluginCommandValidator : AbstractValidator<CreatePluginCommand>
{
    public CreatePluginCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description is not null);

        RuleFor(x => x.SourceUrl)
            .MaximumLength(500)
            .Must(BeValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.SourceUrl));
    }

    private static bool BeValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
