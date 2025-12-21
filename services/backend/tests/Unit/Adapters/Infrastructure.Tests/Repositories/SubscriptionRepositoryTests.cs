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

    [Fact]
    public async Task UpdateWithVersionCheckAsync_WorksWithAsNoTracking_DetachedEntity()
    {
        // This test validates the full flow that happens in ConsumeCreditsHandler:
        // 1. GetByUserIdAsync (with AsNoTracking) returns a detached entity
        // 2. Entity is modified
        // 3. UpdateWithVersionCheckAsync should attach and save the entity
        var dbName = $"SubRepo_{Guid.NewGuid()}";
        var userId = Guid.NewGuid();

        // Setup: Create subscription in one context
        await using (var setupContext = CreateContext(dbName))
        {
            var plan = new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 10,
                Credits = 100
            };
            await setupContext.SubscriptionPlans.AddAsync(plan);

            var subscription = new SubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Credits = 50,
                SubscriptionPlanId = plan.Id,
                SubscriptionPlan = plan,
                Version = 0
            };
            await setupContext.Subscriptions.AddAsync(subscription);
            await setupContext.SaveChangesAsync();
        }

        // Act: Use a new context (simulating the handler flow)
        await using var context = CreateContext(dbName);
        var repo = new SubscriptionRepository(context);

        // Get entity with AsNoTracking (detached)
        var detachedSubscription = await repo.GetByUserIdAsync(userId);
        Assert.NotNull(detachedSubscription);
        Assert.NotNull(detachedSubscription.SubscriptionPlan); // Verify Include worked

        var expectedVersion = detachedSubscription.Version; // 0
        var originalCredits = detachedSubscription.Credits;

        // Modify the detached entity (simulates ConsumeCredits)
        detachedSubscription.ConsumeCredits(10);

        // This should work even with a detached entity
        await repo.UpdateWithVersionCheckAsync(detachedSubscription, expectedVersion);

        // Verify: Check that changes were persisted
        await using var verifyContext = CreateContext(dbName);
        var updated = await verifyContext.Subscriptions.FirstAsync(s => s.UserId == userId);
        Assert.Equal(originalCredits - 10, updated.Credits);
        Assert.Equal(1, updated.Version);
    }
}
