using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public static class DatabaseSeeder
{
    /// <summary>
    /// Pre-computed password hashes for seed users.
    /// These are PBKDF2-SHA256 hashes with 100,000 iterations.
    /// Format: iterations.salt.key
    /// </summary>
    /// <remarks>
    /// These are only used for development/staging seed data.
    /// Pre-computing saves ~400ms on startup (4 hashes × ~100ms each).
    /// </remarks>
    private static class SeedPasswordHashes
    {
        public const string Guest = "100000.S/nPgBCgPPwtOW/LVo2I6g==.JaV/EaFidVmP7mDwr75xOghRHXVAJBaftkd4p2moVsU=";
        public const string User = "100000.hnRQokL5hNnWMmsf5VaJ+A==.Bkf92QnMVJ6/NVP7QzfA6W7oA47Y2GZEmPCBUwUWAuY=";
        public const string Moderator = "100000.vv62xwn/SjDa7tsiytaFVQ==.UZW95qL93VsJTHPd14tkyG2rxw+POADuAbZEKATQ+w0=";
        public const string Admin = "100000.2xEOcVb71Pd73eHf7he64A==.bD3e3mXnALoXmC+1LHdJ/uqWMC32kle8rSjKbhsUCUA=";
    }

    public static async Task SeedAsync(
        ApplicationDbContext db,
        IPasswordHasher hasher,
        IFileStorageService? fileStorage = null,
        IServiceProvider? serviceProvider = null)
    {
        Guid? devAdminId = null;
        if (serviceProvider != null)
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var devUserIdValue = configuration?["Auth:DevUserId"];
            if (!string.IsNullOrWhiteSpace(devUserIdValue) &&
                Guid.TryParse(devUserIdValue, out var parsedId))
            {
                devAdminId = parsedId;
            }
        }

        // Note: Migrations are handled separately via --migrate-only flag (init container)
        // or EnsureCreated for development. Do not call MigrateAsync() here.

        // Seed core entities (roles, users, subscriptions) - consolidated saves
        await SeedCoreEntitiesAsync(db, devAdminId);

        // Seed reference data (plans, costs, plugins) - consolidated saves
        await SeedReferenceDataAsync(db);

        // Seed content (workflows, personalities, agents) - parallelized
        await SeedContentAsync(db, serviceProvider);
    }

    private static async Task SeedCoreEntitiesAsync(ApplicationDbContext db, Guid? devAdminId)
    {
        if (await db.Roles.AnyAsync())
            return;

        // Create all entities first, then save once
        var roles = new[]
        {
            new RoleEntity { Id = Guid.NewGuid(), Name = "guest" },
            new RoleEntity { Id = Guid.NewGuid(), Name = "user" },
            new RoleEntity { Id = Guid.NewGuid(), Name = "moderator" },
            new RoleEntity { Id = Guid.NewGuid(), Name = "admin" }
        };

        // Use pre-computed password hashes to avoid ~400ms of PBKDF2 computation
        var users = new[]
        {
            new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = "guest@example.com",
                Name = "guest",
                PasswordHash = SeedPasswordHashes.Guest,
                Credits = 5
            },
            new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = "user@example.com",
                Name = "user",
                PasswordHash = SeedPasswordHashes.User
            },
            new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = "moderator@example.com",
                Name = "moderator",
                PasswordHash = SeedPasswordHashes.Moderator
            },
            new UserEntity
            {
                Id = devAdminId ?? Guid.NewGuid(),
                Email = "admin@example.com",
                Name = "admin",
                PasswordHash = SeedPasswordHashes.Admin
            }
        };

        var userRoles = users.Select(u => new UserRoleEntity
        {
            UserId = u.Id,
            RoleId = roles.First(r => r.Name == u.Name).Id
        }).ToList();

        var now = DateTime.UtcNow;
        var subscriptions = users
            .Where(u => u.Name != "guest")
            .Select(u => new SubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = u.Id,
                Credits = 10,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();

        // Add all entities
        db.Roles.AddRange(roles);
        db.Users.AddRange(users);
        db.UserRoles.AddRange(userRoles);
        db.Subscriptions.AddRange(subscriptions);

        // Single SaveChangesAsync for all core entities
        await db.SaveChangesAsync();
    }

    private static async Task SeedReferenceDataAsync(ApplicationDbContext db)
    {
        var hasPlans = await db.SubscriptionPlans.AnyAsync();
        var hasCosts = await db.ActionCosts.AnyAsync();
        var hasPlugins = await db.Plugins.AnyAsync();
        var hasCreditPackages = await db.CreditPackages.AnyAsync();
        var existingFeatureFlagKeys = await db.FeatureFlags
            .Select(flag => flag.Key)
            .ToListAsync();

        var hasChanges = false;

        if (!hasPlans)
        {
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Monthly100,
                    Price = 10m,
                    Credits = 100,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Package5,
                    Price = 3m,
                    Credits = 5,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Package10,
                    Price = 5m,
                    Credits = 10,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Package25,
                    Price = 10m,
                    Credits = 25,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.Unlimited,
                    Price = 99m,
                    Credits = 0,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new SubscriptionPlanEntity
                {
                    Id = Guid.NewGuid(),
                    PlanEnum = SubscriptionPlanEnum.InternalTesters,
                    Price = 0m,
                    Credits = 1000,  // Generous credits for internal testing
                    PaymentUnit = PaymentUnit.PerMessage
                }
            );
            hasChanges = true;
        }

        if (!hasCosts)
        {
            db.ActionCosts.AddRange(
                new ActionCostEntity
                {
                    Id = Guid.NewGuid(),
                    ActionName = "chat",
                    Credits = 1,
                    PaymentUnit = PaymentUnit.PerMessage
                },
                new ActionCostEntity
                {
                    Id = Guid.NewGuid(),
                    ActionName = "upload_kb",
                    Credits = 1,
                    PaymentUnit = PaymentUnit.PerMessage
                }
            );
            hasChanges = true;
        }

        if (!hasPlugins)
        {
            db.Plugins.AddRange(
                new PluginEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Calendar Sync",
                    Description = "Synchronize your calendar events",
                    IsActive = true
                },
                new PluginEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "CRM Connector",
                    Description = "Import contacts from your CRM",
                    IsActive = true
                }
            );
            hasChanges = true;
        }

        // Seed credit packages for Stripe integration
        // NOTE: Replace StripePriceId values with actual Stripe Price IDs from your Stripe Dashboard
        // For test mode, create products at https://dashboard.stripe.com/test/products
        if (!hasCreditPackages)
        {
            db.CreditPackages.AddRange(
                new CreditPackageEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Starter Pack",
                    Credits = 100,
                    PriceInCents = 999,  // $9.99
                    Currency = "usd",
                    StripePriceId = "price_starter_100",  // TODO: Replace with actual Stripe Price ID
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new CreditPackageEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Pro Pack",
                    Credits = 500,
                    PriceInCents = 3999,  // $39.99
                    Currency = "usd",
                    StripePriceId = "price_pro_500",  // TODO: Replace with actual Stripe Price ID
                    IsActive = true,
                    DisplayOrder = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new CreditPackageEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Enterprise Pack",
                    Credits = 2000,
                    PriceInCents = 9999,  // $99.99
                    Currency = "usd",
                    StripePriceId = "price_enterprise_2000",  // TODO: Replace with actual Stripe Price ID
                    IsActive = true,
                    DisplayOrder = 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
            hasChanges = true;
        }

        var now = DateTime.UtcNow;
        var existingFeatureFlagKeySet = new HashSet<string>(existingFeatureFlagKeys, StringComparer.OrdinalIgnoreCase);
        var featureFlagsToAdd = new List<FeatureFlagEntity>();

        // Chat V2 feature flags - controlled via InternalTesters whitelist plan
        if (!existingFeatureFlagKeySet.Contains("new-chat-ui"))
        {
            featureFlagsToAdd.Add(new FeatureFlagEntity
            {
                Id = Guid.NewGuid(),
                Key = "new-chat-ui",
                Description = "Enable new Chat V2 UI interface",
                IsEnabled = false,
                AllowedPlans = "InternalTesters",
                RolloutPercentage = 0,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!existingFeatureFlagKeySet.Contains("chat-v2-streaming"))
        {
            featureFlagsToAdd.Add(new FeatureFlagEntity
            {
                Id = Guid.NewGuid(),
                Key = "chat-v2-streaming",
                Description = "Enable Chat V2 streaming responses",
                IsEnabled = false,
                AllowedPlans = "InternalTesters",
                RolloutPercentage = 0,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!existingFeatureFlagKeySet.Contains("advanced_analytics"))
        {
            featureFlagsToAdd.Add(new FeatureFlagEntity
            {
                Id = Guid.NewGuid(),
                Key = "advanced_analytics",
                Description = "Advanced analytics dashboard",
                IsEnabled = false,
                AllowedPlans = "pro,enterprise",
                RolloutPercentage = 100,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!existingFeatureFlagKeySet.Contains("team_collaboration"))
        {
            featureFlagsToAdd.Add(new FeatureFlagEntity
            {
                Id = Guid.NewGuid(),
                Key = "team_collaboration",
                Description = "Team collaboration features",
                IsEnabled = false,
                AllowedPlans = "enterprise",
                RolloutPercentage = 100,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!existingFeatureFlagKeySet.Contains("beta_features"))
        {
            featureFlagsToAdd.Add(new FeatureFlagEntity
            {
                Id = Guid.NewGuid(),
                Key = "beta_features",
                Description = "Beta features access",
                IsEnabled = false,
                RolloutPercentage = 10,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (featureFlagsToAdd.Count > 0)
        {
            db.FeatureFlags.AddRange(featureFlagsToAdd);
            hasChanges = true;
        }

        if (hasChanges)
        {
            // Single SaveChangesAsync for all reference data
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedContentAsync(ApplicationDbContext db, IServiceProvider? serviceProvider)
    {
        // Check which seeders need to run
        var needsWorkflows = !await db.Workflows.AnyAsync();
        var needsPersonalities = !await db.PersonalityTemplates.AnyAsync();
        var needsAgents = !await db.Agents.AnyAsync();
        var needsServiceAccounts = !await db.ServiceAccounts.AnyAsync();

        if (!needsWorkflows && !needsPersonalities && !needsAgents && !needsServiceAccounts)
            return;

        // Run seeders sequentially - DbContext is not thread-safe
        // Personality templates must run before agents since agents reference them
        if (needsPersonalities)
        {
            var logger = serviceProvider?.GetService<ILogger<PersonalityTemplateSeeder>>()
                ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<PersonalityTemplateSeeder>();
            var seeder = new PersonalityTemplateSeeder(db, logger);
            await seeder.SeedAsync();
        }

        if (needsAgents)
        {
            var logger = serviceProvider?.GetService<ILogger<AgentSeeder>>()
                ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSeeder>();
            var seeder = new AgentSeeder(db, logger);
            await seeder.SeedAsync();
        }

        if (needsWorkflows)
        {
            var logger = serviceProvider?.GetService<ILogger<WorkflowSeeder>>()
                ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowSeeder>();
            var seeder = new WorkflowSeeder(db, logger);
            await seeder.SeedAsync();
        }

        if (needsServiceAccounts)
        {
            var logger = serviceProvider?.GetService<ILogger<ServiceAccountSeeder>>()
                ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceAccountSeeder>();
            var hasher = serviceProvider?.GetRequiredService<IPasswordHasher>()
                ?? throw new InvalidOperationException("IPasswordHasher is required for service account seeding");
            var seeder = new ServiceAccountSeeder(db, hasher, logger);
            await seeder.SeedAsync();
        }
    }
}
