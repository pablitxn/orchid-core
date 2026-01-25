using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Integration.Shared.Fixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests.Matrix;

[Collection("Integration")]
public class ValidateMatrixLinkCodeIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ValidateMatrixLinkCodeIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateServiceAccountClient(params string[] scopes)
    {
        ServiceAccountTestAuthHandler.Scopes = scopes;
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication("ServiceAccount")
                    .AddScheme<AuthenticationSchemeOptions, ServiceAccountTestAuthHandler>("ServiceAccount", _ => { });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task ValidateLinkCode_ReturnsSuccess_WhenCodeIsValid()
    {
        // Arrange
        await using var db = _factory.CreateDbContext();
        var userId = Guid.NewGuid();
        var userName = "Test User";
        var userEmail = "testvalidate@example.com";
        var matrixUserId = "@newuser:matrix.orchid-labs.xyz";

        // Create user
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = userEmail,
            Name = userName
        });

        // Create valid link code
        var code = "ORCHID-TEST";
        var linkCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        db.MatrixLinkCodes.Add(linkCode);
        await db.SaveChangesAsync();

        var client = CreateServiceAccountClient("matrix:write");

        // Act
        var response = await client.PostAsJsonAsync("/api/matrix/link/validate", new
        {
            matrixUserId,
            code
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(userId, Guid.Parse(json.GetProperty("userId").GetString()!));
        Assert.Equal(userName, json.GetProperty("userName").GetString());
        Assert.False(json.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null);

        // Verify link was created
        await using var db2 = _factory.CreateDbContext();
        var createdLink = db2.MatrixUserLinks.FirstOrDefault(l => l.MatrixUserId == matrixUserId);
        Assert.NotNull(createdLink);
        Assert.Equal(userId, createdLink.UserId);
        Assert.True(createdLink.IsActive);

        // Verify code was marked as used
        var usedCode = await db2.MatrixLinkCodes.FindAsync(linkCode.Id);
        Assert.NotNull(usedCode);
        Assert.True(usedCode.IsUsed);
    }

    [Fact]
    public async Task ValidateLinkCode_ReturnsInvalidCode_WhenCodeNotFound()
    {
        // Arrange
        var client = CreateServiceAccountClient("matrix:write");

        // Act
        var response = await client.PostAsJsonAsync("/api/matrix/link/validate", new
        {
            matrixUserId = "@test:matrix.orchid-labs.xyz",
            code = "ORCHID-FAKE"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("INVALID_CODE", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ValidateLinkCode_ReturnsExpiredCode_WhenCodeIsExpired()
    {
        // Arrange
        await using var db = _factory.CreateDbContext();
        var userId = Guid.NewGuid();

        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"expired_{Guid.NewGuid()}@example.com",
            Name = "Expired User"
        });

        var code = $"ORCHID-EXP{DateTime.UtcNow.Ticks % 1000:000}";
        var linkCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1) // Expired
        };
        db.MatrixLinkCodes.Add(linkCode);
        await db.SaveChangesAsync();

        var client = CreateServiceAccountClient("matrix:write");

        // Act
        var response = await client.PostAsJsonAsync("/api/matrix/link/validate", new
        {
            matrixUserId = "@test:matrix.orchid-labs.xyz",
            code
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("EXPIRED_CODE", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ValidateLinkCode_ReturnsCodeAlreadyUsed_WhenCodeWasUsed()
    {
        // Arrange
        await using var db = _factory.CreateDbContext();
        var userId = Guid.NewGuid();

        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"used_{Guid.NewGuid()}@example.com",
            Name = "Used Code User"
        });

        var code = $"ORCHID-USE{DateTime.UtcNow.Ticks % 1000:000}";
        var linkCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            UsedAt = DateTime.UtcNow.AddMinutes(-1) // Already used
        };
        db.MatrixLinkCodes.Add(linkCode);
        await db.SaveChangesAsync();

        var client = CreateServiceAccountClient("matrix:write");

        // Act
        var response = await client.PostAsJsonAsync("/api/matrix/link/validate", new
        {
            matrixUserId = "@test:matrix.orchid-labs.xyz",
            code
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("CODE_ALREADY_USED", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ValidateLinkCode_ReturnsAlreadyLinked_WhenMatrixUserAlreadyLinked()
    {
        // Arrange
        await using var db = _factory.CreateDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var matrixUserId = $"@linked_{Guid.NewGuid():N}:matrix.orchid-labs.xyz";

        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"link1_{Guid.NewGuid()}@example.com",
            Name = "Link User 1"
        });

        db.Users.Add(new UserEntity
        {
            Id = otherUserId,
            Email = $"link2_{Guid.NewGuid()}@example.com",
            Name = "Link User 2"
        });

        // Create existing active link for the Matrix user
        db.MatrixUserLinks.Add(new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            MatrixUserId = matrixUserId,
            IsActive = true,
            LinkedAt = DateTime.UtcNow
        });

        var code = $"ORCHID-LNK{DateTime.UtcNow.Ticks % 1000:000}";
        var linkCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        db.MatrixLinkCodes.Add(linkCode);
        await db.SaveChangesAsync();

        var client = CreateServiceAccountClient("matrix:write");

        // Act
        var response = await client.PostAsJsonAsync("/api/matrix/link/validate", new
        {
            matrixUserId,
            code
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("ALREADY_LINKED", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ValidateLinkCode_ReturnsForbidden_WhenScopeMissing()
    {
        // Arrange - Create client WITHOUT matrix:write scope
        var client = CreateServiceAccountClient("matrix:read"); // Wrong scope

        // Act
        var response = await client.PostAsJsonAsync("/api/matrix/link/validate", new
        {
            matrixUserId = "@test:matrix.orchid-labs.xyz",
            code = "ORCHID-TEST"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidateLinkCode_AllowsLinking_WhenPreviousLinkIsInactive()
    {
        // Arrange
        await using var db = _factory.CreateDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var matrixUserId = $"@relink_{Guid.NewGuid():N}:matrix.orchid-labs.xyz";

        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"relink1_{Guid.NewGuid()}@example.com",
            Name = "Relink User"
        });

        db.Users.Add(new UserEntity
        {
            Id = otherUserId,
            Email = $"relink2_{Guid.NewGuid()}@example.com",
            Name = "Previous User"
        });

        // Create inactive link for the Matrix user
        db.MatrixUserLinks.Add(new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            MatrixUserId = matrixUserId,
            IsActive = false, // Inactive
            LinkedAt = DateTime.UtcNow.AddDays(-1)
        });

        var code = $"ORCHID-REL{DateTime.UtcNow.Ticks % 1000:000}";
        var linkCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        db.MatrixLinkCodes.Add(linkCode);
        await db.SaveChangesAsync();

        var client = CreateServiceAccountClient("matrix:write");

        // Act
        var response = await client.PostAsJsonAsync("/api/matrix/link/validate", new
        {
            matrixUserId,
            code
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(userId, Guid.Parse(json.GetProperty("userId").GetString()!));
    }
}
