using System.Net.Http.Json;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditSystem.IntegrationTests;

/// <summary>
/// Base class for CreditSystem integration tests.
/// Uses a shared WebApplicationFactory via ICollectionFixture to prevent ObjectDisposedException.
/// </summary>
public abstract class CreditSystemTestBase
{
    protected readonly CreditSystemWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected CreditSystemTestBase(CreditSystemWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected ApplicationDbContext CreateTestContext()
    {
        return Factory.CreateDbContext();
    }

    protected async Task<string> GenerateTestToken(Guid userId, string email = "test@example.com", string[]? roles = null)
    {
        await using var context = CreateTestContext();

        // Create test user if not exists
        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            user = new UserEntity
            {
                Id = userId,
                Email = email,
                Name = "Test User",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }

        // For integration tests, we'll create a simple test token
        // In a real scenario, this would use the JWT service
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId.ToString()));
    }
}
