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
    private const string DevMatrixBridgeSecretHash = "100000.K8r7xvQ1P5N3jY2L0M4W6A==.5hFdT9uW2vC1xZ8yB3nK7pR4mJ6oL0aS9eQ2iU5wG1c=";

    /// <summary>
    /// Environment variable name for production Matrix Bridge secret hash.
    /// If set, this hash will be used instead of the dev hash.
    /// Generate with: dotnet run --project tools/HashGenerator -- "your-secret"
    /// </summary>
    private const string MatrixBridgeSecretHashEnvVar = "MATRIX_BRIDGE_SECRET_HASH";

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

        var secretHash = Environment.GetEnvironmentVariable(MatrixBridgeSecretHashEnvVar);
        var isProduction = !string.IsNullOrEmpty(secretHash);

        if (!isProduction)
        {
            secretHash = DevMatrixBridgeSecretHash;
            _logger.LogInformation("Seeding development service accounts (using dev secret)...");
        }
        else
        {
            _logger.LogInformation("Seeding service accounts with production secret...");
        }

        var matrixBridge = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "matrix-bridge",
            ClientSecretHash = secretHash,
            Name = isProduction ? "Matrix Bridge" : "Matrix Bridge Development",
            Scopes = new[] { "matrix:read", "matrix:write" },
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.ServiceAccounts.Add(matrixBridge);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Seeded service account: matrix-bridge (production: {IsProduction})", isProduction);
    }
}
