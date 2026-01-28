namespace Domain.Exceptions;

public class WorkspaceSlugAlreadyExistsException : ValidationException
{
    public WorkspaceSlugAlreadyExistsException(string slug)
        : base($"Workspace slug '{slug}' already exists.", nameof(slug))
    {
        Slug = slug;
    }

    public string Slug { get; }
}
