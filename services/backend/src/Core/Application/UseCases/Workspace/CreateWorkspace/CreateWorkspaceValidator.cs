using Domain.Exceptions;

namespace Application.UseCases.Workspace.CreateWorkspace;

public static class CreateWorkspaceValidator
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 1000;
    private const int MaxSlugLength = 100;

    public static void Validate(CreateWorkspaceCommand command)
    {
        if (command == null)
            throw new ValidationException("Command cannot be null.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Name is required.", nameof(command.Name));

        if (command.Name.Length < MinNameLength)
            throw new ValidationException($"Name must be at least {MinNameLength} characters.", nameof(command.Name));

        if (command.Name.Length > MaxNameLength)
            throw new ValidationException($"Name cannot exceed {MaxNameLength} characters.", nameof(command.Name));

        if (command.Description?.Length > MaxDescriptionLength)
            throw new ValidationException($"Description cannot exceed {MaxDescriptionLength} characters.", nameof(command.Description));

        if (command.Slug?.Length > MaxSlugLength)
            throw new ValidationException($"Slug cannot exceed {MaxSlugLength} characters.", nameof(command.Slug));

        if (!string.IsNullOrWhiteSpace(command.AvatarUrl) && !IsValidUrl(command.AvatarUrl))
            throw new ValidationException("Avatar URL must be a valid URL.", nameof(command.AvatarUrl));

        if (command.UserId == Guid.Empty)
            throw new ValidationException("User ID is required.", nameof(command.UserId));
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
