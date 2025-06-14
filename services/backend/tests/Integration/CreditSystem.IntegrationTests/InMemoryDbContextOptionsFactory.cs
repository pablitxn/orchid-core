using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditSystem.IntegrationTests;

public static class InMemoryDbContextOptionsFactory
{
    public static DbContextOptions<ApplicationDbContext> Create()
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        builder.UseInMemoryDatabase($"InMemoryDbForTesting_{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .ConfigureWarnings(w =>
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
            });
        
        return builder.Options;
    }
}