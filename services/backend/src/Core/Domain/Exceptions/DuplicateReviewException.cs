namespace Domain.Exceptions;

public class DuplicateReviewException : Exception
{
    public DuplicateReviewException(Guid userId, Guid pluginId)
        : base($"User '{userId}' has already reviewed plugin '{pluginId}'.")
    {
        UserId = userId;
        PluginId = pluginId;
    }

    public Guid UserId { get; }
    public Guid PluginId { get; }
}
