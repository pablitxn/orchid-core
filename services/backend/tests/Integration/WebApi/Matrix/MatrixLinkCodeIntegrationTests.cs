using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Integration.Shared.Fixtures;

namespace WebApi.IntegrationTests.Matrix;

[Collection("Integration")]
public class MatrixLinkCodeIntegrationTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public MatrixLinkCodeIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateLinkCode_ReturnsCode_WhenUserNotLinked()
    {
        // Arrange - ensure user exists and has no active Matrix link
        await using var db = _factory.CreateDbContext();
        var userId = GuidTestAuthHandler.UserId;

        if (!db.Users.Any(u => u.Id == userId))
        {
            db.Users.Add(new UserEntity
            {
                Id = userId,
                Email = "test@example.com",
                Name = "Test User"
            });
            await db.SaveChangesAsync();
        }

        // Remove any existing Matrix links for this user
        var existingLinks = db.MatrixUserLinks.Where(l => l.UserId == userId).ToList();
        db.MatrixUserLinks.RemoveRange(existingLinks);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.PostAsync("/api/matrix/link/generate", null);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(content).RootElement;

        // Try both casing options (ASP.NET Core default is camelCase)
        var hasCode = json.TryGetProperty("code", out var code) || json.TryGetProperty("Code", out code);
        Assert.True(hasCode, $"Response should contain 'code' property. Actual: {content}");
        Assert.StartsWith("ORCHID-", code.GetString());

        var hasExpiresAt = json.TryGetProperty("expiresAt", out var expiresAt) || json.TryGetProperty("ExpiresAt", out expiresAt);
        Assert.True(hasExpiresAt, $"Response should contain 'expiresAt' property. Actual: {content}");

        var parsed = DateTime.TryParse(expiresAt.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var expirationTime);
        Assert.True(parsed, $"Could not parse expiresAt: {expiresAt.GetString()}");
        Assert.True(expirationTime > DateTime.UtcNow, $"Expiration time {expirationTime} should be in the future");
    }

    [Fact]
    public async Task GenerateLinkCode_ReturnsBadRequest_WhenUserAlreadyLinked()
    {
        // Arrange - create user with active Matrix link
        await using var db = _factory.CreateDbContext();
        var userId = Guid.NewGuid();
        var matrixUserId = "@testuser:matrix.orchid-labs.xyz";

        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"linked_{Guid.NewGuid()}@example.com",
            Name = "Linked User"
        });

        db.MatrixUserLinks.Add(new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatrixUserId = matrixUserId,
            IsActive = true,
            LinkedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        // Create client with custom auth for this user
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // This user-specific auth would need custom handling
            });
        }).CreateClient();

        // Use the default test auth which uses GuidTestAuthHandler.UserId
        // So we need to create the link for that user instead
        await using var db2 = _factory.CreateDbContext();
        var defaultUserId = GuidTestAuthHandler.UserId;

        // Remove any existing links first
        var existingLinks = db2.MatrixUserLinks.Where(l => l.UserId == defaultUserId).ToList();
        db2.MatrixUserLinks.RemoveRange(existingLinks);
        await db2.SaveChangesAsync();

        // Create a new active link
        db2.MatrixUserLinks.Add(new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = defaultUserId,
            MatrixUserId = "@default:matrix.orchid-labs.xyz",
            IsActive = true,
            LinkedAt = DateTime.UtcNow
        });
        await db2.SaveChangesAsync();

        // Act
        var response = await _client.PostAsync("/api/matrix/link/generate", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out var error));
        Assert.Equal("ALREADY_LINKED", error.GetString());
        Assert.True(json.TryGetProperty("matrixUserId", out var matrixId));
        Assert.Equal("@default:matrix.orchid-labs.xyz", matrixId.GetString());

        // Cleanup
        await using var db3 = _factory.CreateDbContext();
        var linksToRemove = db3.MatrixUserLinks.Where(l => l.UserId == defaultUserId).ToList();
        db3.MatrixUserLinks.RemoveRange(linksToRemove);
        await db3.SaveChangesAsync();
    }

    [Fact]
    public async Task GenerateLinkCode_InvalidatesPreviousCode()
    {
        // Arrange
        await using var db = _factory.CreateDbContext();
        var userId = GuidTestAuthHandler.UserId;

        if (!db.Users.Any(u => u.Id == userId))
        {
            db.Users.Add(new UserEntity
            {
                Id = userId,
                Email = "test@example.com",
                Name = "Test User"
            });
        }

        // Remove any existing Matrix links for clean test
        var existingLinks = db.MatrixUserLinks.Where(l => l.UserId == userId).ToList();
        db.MatrixUserLinks.RemoveRange(existingLinks);

        // Create an existing active code
        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = "ORCHID-OLD1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        db.MatrixLinkCodes.Add(existingCode);
        await db.SaveChangesAsync();

        // Act - generate new code
        var response = await _client.PostAsync("/api/matrix/link/generate", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify old code is now marked as used
        await using var db2 = _factory.CreateDbContext();
        var oldCode = await db2.MatrixLinkCodes.FindAsync(existingCode.Id);
        Assert.NotNull(oldCode);
        Assert.True(oldCode.IsUsed);
    }

    [Fact]
    public async Task GenerateLinkCode_CodeHasCorrectFormat()
    {
        // Arrange
        await using var db = _factory.CreateDbContext();
        var userId = GuidTestAuthHandler.UserId;

        if (!db.Users.Any(u => u.Id == userId))
        {
            db.Users.Add(new UserEntity
            {
                Id = userId,
                Email = "test@example.com",
                Name = "Test User"
            });
            await db.SaveChangesAsync();
        }

        // Remove any existing Matrix links for clean test
        var existingLinks = db.MatrixUserLinks.Where(l => l.UserId == userId).ToList();
        db.MatrixUserLinks.RemoveRange(existingLinks);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.PostAsync("/api/matrix/link/generate", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var code = json.GetProperty("code").GetString();

        // Verify format: ORCHID-[A-Z0-9]{4}
        Assert.NotNull(code);
        Assert.Matches(@"^ORCHID-[A-Z0-9]{4}$", code);
    }

    [Fact]
    public async Task GenerateLinkCode_ExpiresInFiveMinutes()
    {
        // Arrange
        await using var db = _factory.CreateDbContext();
        var userId = GuidTestAuthHandler.UserId;

        if (!db.Users.Any(u => u.Id == userId))
        {
            db.Users.Add(new UserEntity
            {
                Id = userId,
                Email = "test@example.com",
                Name = "Test User"
            });
            await db.SaveChangesAsync();
        }

        // Remove any existing Matrix links for clean test
        var existingLinks = db.MatrixUserLinks.Where(l => l.UserId == userId).ToList();
        db.MatrixUserLinks.RemoveRange(existingLinks);
        await db.SaveChangesAsync();

        // Act
        var beforeRequest = DateTime.UtcNow;
        var response = await _client.PostAsync("/api/matrix/link/generate", null);
        var afterRequest = DateTime.UtcNow;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var expiresAtString = json.GetProperty("expiresAt").GetString();
        var expiresAt = DateTime.Parse(expiresAtString!, null, System.Globalization.DateTimeStyles.RoundtripKind);

        // Should expire approximately 5 minutes from now (with some tolerance)
        var expectedMinExpiration = beforeRequest.AddMinutes(MatrixLinkCodeEntity.ExpirationMinutes);
        var expectedMaxExpiration = afterRequest.AddMinutes(MatrixLinkCodeEntity.ExpirationMinutes).AddSeconds(5);

        Assert.True(expiresAt >= expectedMinExpiration.AddSeconds(-1), $"Expiration {expiresAt} should be >= {expectedMinExpiration}");
        Assert.True(expiresAt <= expectedMaxExpiration, $"Expiration {expiresAt} should be <= {expectedMaxExpiration}");
    }
}
