using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Seeders;

public class ServiceAccountSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ServiceAccountSeeder> _logger;

    /// <summary>
    /// Pre-computed password hash for "dev-secret-matrix-bridge".
    /// PBKDF2-SHA256 with 100,000 iterations.
    /// Format: iterations.salt.key
    /// </summary>
    private const string MatrixBridgeSecretHash = "100000.K8r7xvQ1P5N3jY2L0M4W6A==.5hFdT9uW2vC1xZ8yB3nK7pR4mJ6oL0aS9eQ2iU5wG1c=";

    public ServiceAccountSeeder(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ILogger<ServiceAccountSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (await _db.ServiceAccounts.AnyAsync())
        {
            _logger.LogDebug("Service accounts already seeded");
            return;
        }

        _logger.LogInformation("Seeding development service accounts...");

        var matrixBridge = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "matrix-bridge",
            ClientSecretHash = MatrixBridgeSecretHash,
            Name = "Matrix Bridge Development",
            Scopes = new[] { "matrix:read", "matrix:write" },
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.ServiceAccounts.Add(matrixBridge);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Seeded 1 development service account: matrix-bridge");
    }
}
