namespace Application.UseCases.Agent.CreateAgent;

/// <summary>
/// Validator for CreateAgentCommand.
/// </summary>
public static class CreateAgentCommandValidator
{
    private const int MinNameLength = 3;
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 1000;
    private const int MaxAvatarUrlLength = 500;
    private const int MaxLanguageLength = 10;

    public static void Validate(CreateAgentCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Name is required.", nameof(command.Name));

        if (command.Name.Length < MinNameLength)
            throw new ArgumentException($"Name must be at least {MinNameLength} characters.", nameof(command.Name));

        if (command.Name.Length > MaxNameLength)
            throw new ArgumentException($"Name cannot exceed {MaxNameLength} characters.", nameof(command.Name));

        if (command.Description?.Length > MaxDescriptionLength)
            throw new ArgumentException($"Description cannot exceed {MaxDescriptionLength} characters.", nameof(command.Description));

        if (command.AvatarUrl != null)
        {
            if (command.AvatarUrl.Length > MaxAvatarUrlLength)
                throw new ArgumentException($"AvatarUrl cannot exceed {MaxAvatarUrlLength} characters.", nameof(command.AvatarUrl));

            if (!string.IsNullOrEmpty(command.AvatarUrl) && !IsValidUrl(command.AvatarUrl))
                throw new ArgumentException("AvatarUrl must be a valid URL.", nameof(command.AvatarUrl));
        }

        if (command.Language?.Length > MaxLanguageLength)
            throw new ArgumentException($"Language cannot exceed {MaxLanguageLength} characters.", nameof(command.Language));
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
