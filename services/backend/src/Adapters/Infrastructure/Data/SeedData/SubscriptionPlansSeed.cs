using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.SeedData;

public static class SubscriptionPlansSeed
{
    public static async Task SeedSubscriptionPlansAsync(ApplicationDbContext context)
    {
        if (!await context.SubscriptionPlans.AnyAsync())
        {
            var plans = new List<SubscriptionPlanEntity>
            {
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Package5,
                    Price = 4.99m,
                    Credits = 5,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Package10,
                    Price = 9.99m,
                    Credits = 10,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Package25,
                    Price = 19.99m,
                    Credits = 25,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Monthly100,
                    Price = 29.99m,
                    Credits = 100,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Unlimited,
                    Price = 99.99m,
                    Credits = 999999, // High number to represent unlimited
                    PaymentUnit = PaymentUnit.PerMessage
                }
            };

            await context.SubscriptionPlans.AddRangeAsync(plans);
            await context.SaveChangesAsync();
        }
    }
}