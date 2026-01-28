namespace Domain.Exceptions;

public class InvalidSemanticVersionException : ValidationException
{
    public InvalidSemanticVersionException(string version)
        : base($"'{version}' is not a valid semantic version. Expected format: MAJOR.MINOR.PATCH[-prerelease][+build]", "version")
    {
        ProvidedVersion = version;
    }

    public string ProvidedVersion { get; }
}
