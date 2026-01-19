using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Integration.Shared.Fixtures;

/// <summary>
/// Factory for creating in-memory database contexts for integration testing.
/// </summary>
public static class InMemoryDbContextFactory
{
    /// <summary>
    /// Creates DbContextOptions configured for an in-memory database with the specified name.
    /// </summary>
    public static DbContextOptions<ApplicationDbContext> CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .ConfigureWarnings(w =>
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
            })
            .Options;
    }

    /// <summary>
    /// Creates a new ApplicationDbContext with an in-memory database.
    /// </summary>
    public static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = CreateOptions(databaseName);
        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Creates a unique database name for test isolation.
    /// </summary>
    public static string GenerateUniqueDatabaseName() => $"TestDb_{Guid.NewGuid()}";
}
