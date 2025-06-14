using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class UserWorkflowEntity
{
    [Key] public Guid UserId { get; init; }
    public UserEntity User { get; init; } = null!;
    [Key] public Guid WorkflowId { get; init; }
    public WorkflowEntity Workflow { get; init; } = null!;
    public DateTime PurchasedAt { get; init; } = DateTime.UtcNow;
}