namespace Domain.Enums;

/// <summary>
/// Represents the status of a credit package purchase transaction.
/// </summary>
public enum CreditPurchaseStatus
{
    /// <summary>
    /// Purchase initiated but payment not yet completed.
    /// </summary>
    Pending,

    /// <summary>
    /// Payment completed and credits granted.
    /// </summary>
    Completed,

    /// <summary>
    /// Payment failed or was declined.
    /// </summary>
    Failed,

    /// <summary>
    /// Purchase was refunded.
    /// </summary>
    Refunded
}
