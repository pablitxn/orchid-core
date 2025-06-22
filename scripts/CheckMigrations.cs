using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Infrastructure.Persistence;

class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = "Host=localhost;Port=5433;Database=orchid_core;Username=admin;Password=admin";
        
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());
        
        using var context = new ApplicationDbContext(optionsBuilder.Options, configuration);
        
        try
        {
            Console.WriteLine("Checking database connection...");
            var canConnect = await context.Database.CanConnectAsync();
            Console.WriteLine($"Can connect to database: {canConnect}");
            
            if (canConnect)
            {
                Console.WriteLine("\nChecking migration history...");
                var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
                Console.WriteLine($"Applied migrations count: {appliedMigrations.Count()}");
                
                foreach (var migration in appliedMigrations)
                {
                    Console.WriteLine($"  - {migration}");
                }
                
                Console.WriteLine("\nChecking if migrations are pending...");
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                Console.WriteLine($"Pending migrations count: {pendingMigrations.Count()}");
                
                foreach (var migration in pendingMigrations)
                {
                    Console.WriteLine($"  - {migration}");
                }
                
                // Try to list tables using raw SQL
                Console.WriteLine("\nListing database tables:");
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = @"
                    SELECT table_name 
                    FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                    ORDER BY table_name";
                
                await context.Database.OpenConnectionAsync();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Console.WriteLine($"  - {reader.GetString(0)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}