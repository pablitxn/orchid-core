using Domain.Enums;

namespace Domain.Entities;

public class ActionCostEntity
{
    public Guid Id { get; set; }
    public required string ActionName { get; set; }
    public required decimal Credits { get; set; }
    public PaymentUnit PaymentUnit { get; set; } = PaymentUnit.PerMessage;
    /// <summary>
    /// Timestamp when the action cost was recorded.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}