namespace Domain.Exceptions;

public class AlreadyPurchasedException : Exception
{
    public AlreadyPurchasedException(string itemType, Guid itemId, Guid userId)
        : base($"User '{userId}' has already purchased {itemType} '{itemId}'.")
    {
        ItemType = itemType;
        ItemId = itemId;
        UserId = userId;
    }

    public string ItemType { get; }
    public Guid ItemId { get; }
    public Guid UserId { get; }
}
