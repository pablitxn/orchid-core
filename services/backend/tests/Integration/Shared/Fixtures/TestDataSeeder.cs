using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Integration.Shared.Fixtures;

/// <summary>
/// Seeds test data at application startup for integration tests.
/// </summary>
public class TestDataSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public TestDataSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await SeedSubscriptionPlansAsync(context, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedSubscriptionPlansAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (await context.SubscriptionPlans.AnyAsync(cancellationToken))
        {
            return;
        }

        var plans = new[]
        {
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 29.99m,
                Credits = 100
            },
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Package5,
                Price = 4.99m,
                Credits = 5
            },
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Package10,
                Price = 9.99m,
                Credits = 10
            },
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Package25,
                Price = 19.99m,
                Credits = 25
            },
            new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Unlimited,
                Price = 99.99m,
                Credits = 0
            }
        };

        context.SubscriptionPlans.AddRange(plans);
        await context.SaveChangesAsync(cancellationToken);
    }
}
