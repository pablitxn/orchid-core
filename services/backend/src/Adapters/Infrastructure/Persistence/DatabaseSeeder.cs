using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IPasswordHasher hasher, IFileStorageService? fileStorage = null, IServiceProvider? serviceProvider = null)
    {
        await db.Database.MigrateAsync();

        if (!await db.Roles.AnyAsync())
        {
            var roles = new[]
            {
                new RoleEntity { Id = Guid.NewGuid(), Name = "guest" },
                new RoleEntity { Id = Guid.NewGuid(), Name = "user" },
                new RoleEntity { Id = Guid.NewGuid(), Name = "moderator" },
                new RoleEntity { Id = Guid.NewGuid(), Name = "admin" }
            };
            db.Roles.AddRange(roles);
            await db.SaveChangesAsync();

            var users = new[]
            {
                new UserEntity
                {
                    Id = Guid.NewGuid(), Email = "guest@example.com", Name = "guest",
                    PasswordHash = hasher.HashPassword("guest")
                },
                new UserEntity
                {
                    Id = Guid.NewGuid(), Email = "user@example.com", Name = "user",
                    PasswordHash = hasher.HashPassword("user")
                },
                new UserEntity
                {
                    Id = Guid.NewGuid(), Email = "moderator@example.com", Name = "moderator",
                    PasswordHash = hasher.HashPassword("moderator")
                },
                new UserEntity
                {
                    Id = Guid.NewGuid(), Email = "admin@example.com", Name = "admin",
                    PasswordHash = hasher.HashPassword("admin")
                }
            };
            db.Users.AddRange(users);
            await db.SaveChangesAsync();

            // Assign each seeded user to their corresponding role
            db.UserRoles.AddRange(
                users.Select(u => new UserRoleEntity
                {
                    UserId = u.Id,
                    RoleId = roles.First(r => r.Name == u.Name).Id
                }));
            await db.SaveChangesAsync();

            // Seed default subscriptions for non-guests and adjust guest credits
            var now = DateTime.UtcNow;
            db.Subscriptions.AddRange(
                users.Where(u => u.Name != "guest").Select(u => new SubscriptionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = u.Id,
                    Credits = 100,
                    CreatedAt = now,
                    UpdatedAt = now
                }));

            var guest = users.First(u => u.Name == "guest");
            guest.Credits = 5;
            db.Users.Update(guest);

            await db.SaveChangesAsync();
        }

        if (!await db.SubscriptionPlans.AnyAsync())
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
                }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.ActionCosts.AnyAsync())
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
            await db.SaveChangesAsync();
        }

        if (!await db.Plugins.AnyAsync())
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
            await db.SaveChangesAsync();
        }

        // Seed workflows
        if (!await db.Workflows.AnyAsync())
        {
            // Create a logger for WorkflowSeeder
            var logger = serviceProvider?.GetService<ILogger<WorkflowSeeder>>() ?? 
                        new Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowSeeder>();
            
            var workflowSeeder = new WorkflowSeeder(db, logger);
            await workflowSeeder.SeedAsync();
        }

        // Seed default knowledge base files for all users
        if (fileStorage != null && !await db.KnowledgeBaseFiles.AnyAsync())
        {
            var kbSeeder = new KnowledgeBaseSeeder(db, fileStorage);
            await kbSeeder.SeedAllUsersAsync();
        }
        
        // Seed personality templates
        if (!await db.PersonalityTemplates.AnyAsync())
        {
            // Create a logger for PersonalityTemplateSeeder
            var logger = serviceProvider?.GetService<ILogger<PersonalityTemplateSeeder>>() ?? 
                        new Microsoft.Extensions.Logging.Abstractions.NullLogger<PersonalityTemplateSeeder>();
            
            var personalitySeeder = new PersonalityTemplateSeeder(db, logger);
            await personalitySeeder.SeedAsync();
        }

        // Seed agents
        if (!await db.Agents.AnyAsync())
        {
            // Create a logger for AgentSeeder
            var logger = serviceProvider?.GetService<ILogger<AgentSeeder>>() ?? 
                        new Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSeeder>();
            
            var agentSeeder = new AgentSeeder(db, logger);
            await agentSeeder.SeedAsync();
        }
    }
}