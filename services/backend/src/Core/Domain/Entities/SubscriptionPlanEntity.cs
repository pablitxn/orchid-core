using Domain.Enums;

namespace Domain.Entities;

public class SubscriptionPlanEntity
{
    public Guid Id { get; set; }
    public required SubscriptionPlanEnum PlanEnum { get; set; }
    public required decimal Price { get; set; }
    public required int Credits { get; set; }
    public PaymentUnit PaymentUnit { get; set; } = PaymentUnit.PerMessage;
}