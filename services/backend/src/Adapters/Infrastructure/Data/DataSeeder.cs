using Infrastructure.Data.SeedData;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data;

public static class DataSeeder
{
    public static async Task<IHost> SeedDataAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
            
            logger.LogInformation("Starting data seeding...");
            
            // Seed subscription plans
            await SubscriptionPlansSeed.SeedSubscriptionPlansAsync(context);
            
            logger.LogInformation("Data seeding completed successfully");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
            logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
        
        return host;
    }
}