using FluentValidation;
using WebApi.Controllers;

namespace WebApi.Validation;

public sealed class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequest>
{
    private const int MinNameLength = 3;
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 1000;
    private const int MaxAvatarUrlLength = 500;
    private const int MaxLanguageLength = 10;

    public CreateAgentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(MinNameLength)
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(MaxAvatarUrlLength)
            .Must(BeValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl));

        RuleFor(x => x.Language)
            .MaximumLength(MaxLanguageLength)
            .When(x => x.Language is not null);
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

public sealed class UpdateAgentRequestValidator : AbstractValidator<UpdateAgentRequest>
{
    private const int MinNameLength = 3;
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 1000;
    private const int MaxAvatarUrlLength = 500;
    private const int MaxLanguageLength = 10;

    public UpdateAgentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(MinNameLength)
            .MaximumLength(MaxNameLength)
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(MaxAvatarUrlLength)
            .Must(BeValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl));

        RuleFor(x => x.Language)
            .MaximumLength(MaxLanguageLength)
            .When(x => x.Language is not null);
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

public sealed class RateAgentRequestValidator : AbstractValidator<RateAgentRequest>
{
    public RateAgentRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5);

        RuleFor(x => x.Comment)
            .MaximumLength(1000)
            .When(x => x.Comment is not null);
    }
}
