namespace Domain.Exceptions;

public class DuplicatePluginSlugException : Exception
{
    public string Slug { get; }

    public DuplicatePluginSlugException(string slug)
        : base($"A plugin with slug '{slug}' already exists.")
    {
        Slug = slug;
    }
}
