namespace Domain.Exceptions;

public class PluginNotFoundException : EntityNotFoundException
{
    public PluginNotFoundException(Guid pluginId)
        : base("Plugin", pluginId)
    {
        PluginId = pluginId;
    }

    public Guid PluginId { get; }
}
