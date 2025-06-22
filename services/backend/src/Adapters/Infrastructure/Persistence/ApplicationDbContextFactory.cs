using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
///     Design-time DbContext factory for EF Core migrations.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Build configuration to read connection string or environment variable
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        // Try standard connection string or fallback to DB_CONN env var
        var cs = configuration.GetConnectionString("DefaultConnection")
                 ?? configuration["DB_CONN"]
                 ?? "Host=localhost;Database=orchid_core;Username=admin;Password=admin;Port=5433";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(
            cs,
            o => o.UseVector());

        return new ApplicationDbContext(optionsBuilder.Options, configuration);
    }
}