using Domain.Exceptions;

namespace Application.UseCases.Agent.UpdateAgent;

/// <summary>
/// Validator for UpdateAgentCommand.
/// </summary>
public static class UpdateAgentCommandValidator
{
    private const int MinNameLength = 3;
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 1000;
    private const int MaxAvatarUrlLength = 500;
    private const int MaxLanguageLength = 10;

    public static void Validate(UpdateAgentCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        if (command.AgentId == Guid.Empty)
            throw new ValidationException("AgentId cannot be empty.", nameof(command.AgentId));

        if (command.UserId == Guid.Empty)
            throw new ValidationException("UserId cannot be empty.", nameof(command.UserId));

        if (command.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
                throw new ValidationException("Name cannot be empty when provided.", nameof(command.Name));

            if (command.Name.Length < MinNameLength)
                throw new ValidationException($"Name must be at least {MinNameLength} characters.", nameof(command.Name));

            if (command.Name.Length > MaxNameLength)
                throw new ValidationException($"Name cannot exceed {MaxNameLength} characters.", nameof(command.Name));
        }

        if (command.Description?.Length > MaxDescriptionLength)
            throw new ValidationException($"Description cannot exceed {MaxDescriptionLength} characters.", nameof(command.Description));

        if (command.AvatarUrl is not null && !string.IsNullOrEmpty(command.AvatarUrl))
        {
            if (command.AvatarUrl.Length > MaxAvatarUrlLength)
                throw new ValidationException($"AvatarUrl cannot exceed {MaxAvatarUrlLength} characters.", nameof(command.AvatarUrl));

            if (!IsValidUrl(command.AvatarUrl))
                throw new ValidationException("AvatarUrl must be a valid URL.", nameof(command.AvatarUrl));
        }

        if (command.Language?.Length > MaxLanguageLength)
            throw new ValidationException($"Language cannot exceed {MaxLanguageLength} characters.", nameof(command.Language));
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
