namespace Domain.Exceptions;

public class PluginNotOwnedException : Exception
{
    public PluginNotOwnedException(Guid userId, Guid pluginId)
        : base($"User '{userId}' must own plugin '{pluginId}' to submit a review.")
    {
        UserId = userId;
        PluginId = pluginId;
    }

    public Guid UserId { get; }
    public Guid PluginId { get; }
}
