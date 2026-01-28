namespace Domain.Exceptions;

public class IncompatiblePluginVersionException : ValidationException
{
    public IncompatiblePluginVersionException(string currentVersion, string targetVersion)
        : base($"Cannot change from version '{currentVersion}' to incompatible version '{targetVersion}'. Versions must share the same major version for compatibility.", "targetVersion")
    {
        CurrentVersion = currentVersion;
        TargetVersion = targetVersion;
    }

    public string CurrentVersion { get; }
    public string TargetVersion { get; }
}
