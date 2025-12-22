using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.UseCases.Agent.Common;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agent.IntegrationTests;

public class AgentAccessVerificationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _configuredFactory;
    private readonly HttpClient _client;
    private static readonly string TestDbName = "TestDb_Access_" + Guid.NewGuid();

    public AgentAccessVerificationTests(WebApplicationFactory<Program> factory)
    {
        _configuredFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove all EF Core related services
                var efCoreServices = services
                    .Where(d => d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true
                             || d.ServiceType.FullName?.Contains("DbContext") == true
                             || d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                    .ToList();
                foreach (var service in efCoreServices)
                {
                    services.Remove(service);
                }

                // Add InMemory database
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(TestDbName);
                });

                // Add test authentication as default
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, AccessTestAuthHandler>("Test", _ => { });
            });
        });
        _client = _configuredFactory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Access_Own_Private_Agent()
    {
        // Arrange
        using var scope = _configuredFactory.Services.CreateScope();
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
        SetTestUserId(_client, userId);

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
        using var scope = _configuredFactory.Services.CreateScope();
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
        SetTestUserId(_client, otherUserId);

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
        using var scope = _configuredFactory.Services.CreateScope();
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

        SetTestUserId(_client, userId);

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
        using var scope = _configuredFactory.Services.CreateScope();
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

        SetTestUserId(_client, userId);

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
        using var scope = _configuredFactory.Services.CreateScope();
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

        SetTestUserId(_client, userId);

        // Act
        var response = await _client.GetAsync($"/api/agents/{agent.Id}/verify-access");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VerifyAccessResponse>();
        result!.HasAccess.Should().BeTrue();
        result.MissingPlugins.Should().BeNullOrEmpty();
    }

    private static void SetTestUserId(HttpClient client, Guid userId)
    {
        client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
    }

    private class VerifyAccessResponse
    {
        public bool HasAccess { get; set; }
        public string? Reason { get; set; }
        public List<Guid>? MissingPlugins { get; set; }
    }
}

public class AccessTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public AccessTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User-Id", out var userIdHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(userIdHeader.FirstOrDefault(), out var userId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid user ID"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}