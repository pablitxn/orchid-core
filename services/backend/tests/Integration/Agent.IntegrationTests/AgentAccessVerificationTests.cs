using System.Net;
using System.Net.Http.Json;
using Application.UseCases.Agent.Common;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agent.IntegrationTests;

public class AgentAccessVerificationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AgentAccessVerificationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task User_Can_Access_Own_Private_Agent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Name = "Test User"
        };
        
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "My Private Agent",
            IsPublic = false,
            UserId = userId
        };
        
        await context.Users.AddAsync(user);
        await context.Agents.AddAsync(agent);
        await context.SaveChangesAsync();

        // Set up authentication
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(userId)}");

        // Act
        var response = await _client.GetAsync($"/api/agents/{agent.Id}/verify-access");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VerifyAccessResponse>();
        result.Should().NotBeNull();
        result!.HasAccess.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public async Task User_Cannot_Access_Others_Private_Agent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        
        var owner = new UserEntity
        {
            Id = ownerId,
            Email = "owner@example.com",
            Name = "Owner"
        };
        
        var otherUser = new UserEntity
        {
            Id = otherUserId,
            Email = "other@example.com",
            Name = "Other User"
        };
        
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Private Agent",
            IsPublic = false,
            UserId = ownerId
        };
        
        await context.Users.AddRangeAsync(owner, otherUser);
        await context.Agents.AddAsync(agent);
        await context.SaveChangesAsync();

        // Set up authentication as other user
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(otherUserId)}");

        // Act
        var response = await _client.GetAsync($"/api/agents/{agent.Id}/verify-access");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var result = await response.Content.ReadFromJsonAsync<VerifyAccessResponse>();
        result.Should().NotBeNull();
        result!.HasAccess.Should().BeFalse();
        result.Reason.Should().Contain("private");
    }

    [Fact]
    public async Task User_Can_Access_Public_Agent_Without_Plugins()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Public Agent",
            IsPublic = true,
            UserId = ownerId,
            PluginIds = Array.Empty<Guid>()
        };
        
        var user = new UserEntity
        {
            Id = userId,
            Email = "user@example.com",
            Name = "User"
        };
        
        await context.Users.AddAsync(user);
        await context.Agents.AddAsync(agent);
        await context.SaveChangesAsync();

        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(userId)}");

        // Act
        var response = await _client.GetAsync($"/api/agents/{agent.Id}/verify-access");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VerifyAccessResponse>();
        result!.HasAccess.Should().BeTrue();
    }

    [Fact]
    public async Task User_Cannot_Access_Public_Agent_Without_Required_Plugins()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var userId = Guid.NewGuid();
        var plugin1Id = Guid.NewGuid();
        var plugin2Id = Guid.NewGuid();
        
        var user = new UserEntity
        {
            Id = userId,
            Email = "user@example.com",
            Name = "User"
        };
        
        var plugin1 = new PluginEntity
        {
            Id = plugin1Id,
            Name = "Plugin 1",
            SystemName = "plugin1",
            IsActive = true
        };
        
        var plugin2 = new PluginEntity
        {
            Id = plugin2Id,
            Name = "Plugin 2",
            SystemName = "plugin2",
            IsActive = true
        };
        
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Public Agent with Plugins",
            IsPublic = true,
            UserId = Guid.NewGuid(),
            PluginIds = new[] { plugin1Id, plugin2Id }
        };
        
        // User only owns plugin1
        var userPlugin = new UserPluginEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PluginId = plugin1Id,
            PurchasedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        await context.Users.AddAsync(user);
        await context.Plugins.AddRangeAsync(plugin1, plugin2);
        await context.Agents.AddAsync(agent);
        await context.UserPlugins.AddAsync(userPlugin);
        await context.SaveChangesAsync();

        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(userId)}");

        // Act
        var response = await _client.GetAsync($"/api/agents/{agent.Id}/verify-access");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var result = await response.Content.ReadFromJsonAsync<VerifyAccessResponse>();
        result.Should().NotBeNull();
        result!.HasAccess.Should().BeFalse();
        result.Reason.Should().Contain("plugins");
        result.MissingPlugins.Should().HaveCount(1);
        result.MissingPlugins.Should().Contain(plugin2Id);
    }

    [Fact]
    public async Task User_Can_Access_Public_Agent_With_All_Required_Plugins()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var userId = Guid.NewGuid();
        var plugin1Id = Guid.NewGuid();
        var plugin2Id = Guid.NewGuid();
        
        var user = new UserEntity
        {
            Id = userId,
            Email = "user@example.com",
            Name = "User"
        };
        
        var plugins = new[]
        {
            new PluginEntity { Id = plugin1Id, Name = "Plugin 1", SystemName = "plugin1", IsActive = true },
            new PluginEntity { Id = plugin2Id, Name = "Plugin 2", SystemName = "plugin2", IsActive = true }
        };
        
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Public Agent with Plugins",
            IsPublic = true,
            UserId = Guid.NewGuid(),
            PluginIds = new[] { plugin1Id, plugin2Id }
        };
        
        // User owns both plugins
        var userPlugins = new[]
        {
            new UserPluginEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PluginId = plugin1Id,
                PurchasedAt = DateTime.UtcNow,
                IsActive = true
            },
            new UserPluginEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PluginId = plugin2Id,
                PurchasedAt = DateTime.UtcNow,
                IsActive = true
            }
        };
        
        await context.Users.AddAsync(user);
        await context.Plugins.AddRangeAsync(plugins);
        await context.Agents.AddAsync(agent);
        await context.UserPlugins.AddRangeAsync(userPlugins);
        await context.SaveChangesAsync();

        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(userId)}");

        // Act
        var response = await _client.GetAsync($"/api/agents/{agent.Id}/verify-access");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VerifyAccessResponse>();
        result!.HasAccess.Should().BeTrue();
        result.MissingPlugins.Should().BeNullOrEmpty();
    }

    private string GenerateTestToken(Guid userId)
    {
        // In a real scenario, this would generate a proper JWT token
        // For testing, we'll use a simple encoded user ID
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId.ToString()));
    }

    private class VerifyAccessResponse
    {
        public bool HasAccess { get; set; }
        public string? Reason { get; set; }
        public List<Guid>? MissingPlugins { get; set; }
    }
}