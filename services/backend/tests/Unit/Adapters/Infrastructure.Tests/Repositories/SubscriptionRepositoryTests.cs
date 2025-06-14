using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Repositories;

public class SubscriptionRepositoryTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task UpdateWithVersionCheckAsync_PersistsChangesAndIncrementsVersion()
    {
        var dbName = $"SubRepo_{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var repo = new SubscriptionRepository(context);

        var plan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = SubscriptionPlanEnum.Monthly100,
            Price = 10,
            Credits = 100
        };
        await context.SubscriptionPlans.AddAsync(plan);

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Credits = 50,
            SubscriptionPlanId = plan.Id,
            SubscriptionPlan = plan
        };
        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();

        var expectedVersion = subscription.Version; // 0
        subscription.AddCredits(10); // increments version to 1

        await repo.UpdateWithVersionCheckAsync(subscription, expectedVersion);

        var updated = await context.Subscriptions.FirstAsync(s => s.Id == subscription.Id);
        Assert.Equal(60, updated.Credits);
        Assert.Equal(1, updated.Version);
    }

    [Fact]
    public async Task UpdateWithVersionCheckAsync_Throws_When_VersionMismatch()
    {
        var dbName = $"SubRepo_{Guid.NewGuid()}";
        await using var context1 = CreateContext(dbName);
        var repo = new SubscriptionRepository(context1);

        var sub = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Credits = 20
        };
        await context1.Subscriptions.AddAsync(sub);
        await context1.SaveChangesAsync();

        // Simulate concurrent update using separate context
        await using (var context2 = CreateContext(dbName))
        {
            var existing = await context2.Subscriptions.FindAsync(sub.Id);
            existing!.AddCredits(5);
            await context2.SaveChangesAsync();
        }

        var expectedVersion = sub.Version; // 0
        sub.AddCredits(5); // version 1

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            repo.UpdateWithVersionCheckAsync(sub, expectedVersion));
    }
}
