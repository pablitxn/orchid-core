namespace Domain.Exceptions;

public class PluginVersionNotFoundException : EntityNotFoundException
{
    public PluginVersionNotFoundException(Guid versionId)
        : base("PluginVersion", versionId)
    {
        VersionId = versionId;
    }

    public PluginVersionNotFoundException(Guid pluginId, string version)
        : base($"Plugin version '{version}' not found for plugin '{pluginId}'.")
    {
        PluginId = pluginId;
        Version = version;
    }

    public Guid? PluginId { get; }
    public Guid VersionId { get; }
    public string? Version { get; }
}
