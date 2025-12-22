using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.UseCases.Agent.Common;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApi.Controllers;
using Xunit;

namespace Agent.IntegrationTests;

public class AgentOwnershipOperationsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AgentOwnershipOperationsTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    private (WebApplicationFactory<Program> factory, HttpClient client) CreateTestEnvironment(string testName)
    {
        var dbName = $"TestDb_Ownership_{testName}_{Guid.NewGuid()}";
        var configuredFactory = _baseFactory.WithWebHostBuilder(builder =>
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

                // Add InMemory database with unique name
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });

                // Add test authentication as default
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        });
        return (configuredFactory, configuredFactory.CreateClient());
    }

    [Fact]
    public async Task CreateAgent_Should_Assign_Ownership_To_Creating_User()
    {
        // Arrange
        var (factory, client) = CreateTestEnvironment(nameof(CreateAgent_Should_Assign_Ownership_To_Creating_User));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "creator@example.com",
            Name = "Creator"
        };

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        SetTestUserId(client, userId);

        var createRequest = new CreateAgentRequest
        {
            Name = "My New Agent",
            Description = "Test agent",
            IsPublic = false,
            Language = "en"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/agents", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdAgent = await response.Content.ReadFromJsonAsync<AgentDto>(_jsonOptions);
        createdAgent.Should().NotBeNull();
        createdAgent!.UserId.Should().Be(userId);
        createdAgent.IsPublic.Should().BeFalse();
        createdAgent.Name.Should().Be("My New Agent");

        // Verify in database
        var dbAgent = await context.Agents.FirstOrDefaultAsync(a => a.Id == createdAgent.Id);
        dbAgent.Should().NotBeNull();
        dbAgent!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task ListAgents_Should_Return_Public_And_Own_Private_Agents()
    {
        // Arrange
        var (factory, client) = CreateTestEnvironment(nameof(ListAgents_Should_Return_Public_And_Own_Private_Agents));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var users = new[]
        {
            new UserEntity { Id = currentUserId, Email = "current@example.com", Name = "Current User" },
            new UserEntity { Id = otherUserId, Email = "other@example.com", Name = "Other User" }
        };

        var agents = new[]
        {
            new AgentEntity
            {
                Id = Guid.NewGuid(),
                Name = "My Private Agent",
                IsPublic = false,
                UserId = currentUserId
            },
            new AgentEntity
            {
                Id = Guid.NewGuid(),
                Name = "Other's Private Agent",
                IsPublic = false,
                UserId = otherUserId
            },
            new AgentEntity
            {
                Id = Guid.NewGuid(),
                Name = "Public Agent 1",
                IsPublic = true,
                UserId = otherUserId
            },
            new AgentEntity
            {
                Id = Guid.NewGuid(),
                Name = "Public Agent 2",
                IsPublic = true,
                UserId = currentUserId
            }
        };

        await context.Users.AddRangeAsync(users);
        await context.Agents.AddRangeAsync(agents);
        await context.SaveChangesAsync();

        SetTestUserId(client, currentUserId);

        // Act
        var response = await client.GetAsync("/api/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agentList = await response.Content.ReadFromJsonAsync<List<AgentDto>>(_jsonOptions);
        agentList.Should().NotBeNull();
        agentList.Should().HaveCount(3); // Own private + 2 public agents

        agentList.Should().Contain(a => a.Name == "My Private Agent" && !a.IsPublic);
        agentList.Should().Contain(a => a.Name == "Public Agent 1" && a.IsPublic);
        agentList.Should().Contain(a => a.Name == "Public Agent 2" && a.IsPublic);
        agentList.Should().NotContain(a => a.Name == "Other's Private Agent");
    }

    [Fact]
    public async Task DeleteAgent_Should_Succeed_For_Owner()
    {
        // Arrange
        var (factory, client) = CreateTestEnvironment(nameof(DeleteAgent_Should_Succeed_For_Owner));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "owner@example.com",
            Name = "Owner"
        };

        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "My Agent",
            IsPublic = false,
            UserId = userId
        };

        await context.Users.AddAsync(user);
        await context.Agents.AddAsync(agent);
        await context.SaveChangesAsync();

        SetTestUserId(client, userId);

        // Act
        var response = await client.DeleteAsync($"/api/agents/{agent.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Refresh context to get updated data
        await context.Entry(agent).ReloadAsync();

        // Verify soft delete in database
        agent.IsDeleted.Should().BeTrue();
        agent.IsInRecycleBin.Should().BeTrue();
        agent.DeletedAt.Should().NotBeNull();
        agent.RecycleBinExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task DeleteAgent_Should_Fail_For_NonOwner()
    {
        // Arrange
        var (factory, client) = CreateTestEnvironment(nameof(DeleteAgent_Should_Fail_For_NonOwner));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var users = new[]
        {
            new UserEntity { Id = ownerId, Email = "owner@example.com", Name = "Owner" },
            new UserEntity { Id = otherUserId, Email = "other@example.com", Name = "Other" }
        };

        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Owner's Agent",
            IsPublic = true,
            UserId = ownerId
        };

        await context.Users.AddRangeAsync(users);
        await context.Agents.AddAsync(agent);
        await context.SaveChangesAsync();

        SetTestUserId(client, otherUserId);

        // Act
        var response = await client.DeleteAsync($"/api/agents/{agent.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Refresh context to verify agent is still active
        await context.Entry(agent).ReloadAsync();

        // Verify agent is still active in database
        agent.IsDeleted.Should().BeFalse();
        agent.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateAgent_With_IsPublic_True_Should_Be_Accessible_To_Others()
    {
        // Arrange
        var (factory, client) = CreateTestEnvironment(nameof(CreateAgent_With_IsPublic_True_Should_Be_Accessible_To_Others));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creatorId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var users = new[]
        {
            new UserEntity { Id = creatorId, Email = "creator@example.com", Name = "Creator" },
            new UserEntity { Id = otherId, Email = "other@example.com", Name = "Other" }
        };

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        // Create public agent
        client.DefaultRequestHeaders.Clear();
        SetTestUserId(client, creatorId);

        var createRequest = new CreateAgentRequest
        {
            Name = "Public Test Agent",
            Description = "A public agent for testing",
            IsPublic = true,
            Language = "en"
        };

        var createResponse = await client.PostAsJsonAsync("/api/agents", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdAgent = await createResponse.Content.ReadFromJsonAsync<AgentDto>(_jsonOptions);

        // List agents as another user
        client.DefaultRequestHeaders.Clear();
        SetTestUserId(client, otherId);

        // Act
        var listResponse = await client.GetAsync("/api/agents");

        // Assert
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var agentList = await listResponse.Content.ReadFromJsonAsync<List<AgentDto>>(_jsonOptions);
        agentList.Should().Contain(a => a.Id == createdAgent!.Id && a.IsPublic);
    }

    [Fact]
    public async Task Unauthenticated_User_Can_See_Public_Agents_Only()
    {
        // Arrange
        var (factory, client) = CreateTestEnvironment(nameof(Unauthenticated_User_Can_See_Public_Agents_Only));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();

        var agents = new[]
        {
            new AgentEntity
            {
                Id = Guid.NewGuid(),
                Name = "Private Agent",
                IsPublic = false,
                UserId = userId
            },
            new AgentEntity
            {
                Id = Guid.NewGuid(),
                Name = "Public Agent 1",
                IsPublic = true,
                UserId = userId
            },
            new AgentEntity
            {
                Id = Guid.NewGuid(),
                Name = "Public Agent 2",
                IsPublic = true,
                UserId = Guid.NewGuid()
            }
        };

        await context.Agents.AddRangeAsync(agents);
        await context.SaveChangesAsync();

        // No authentication header - ensure no X-Test-User-Id is set
        client.DefaultRequestHeaders.Remove("X-Test-User-Id");

        // Act
        var response = await client.GetAsync("/api/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agentList = await response.Content.ReadFromJsonAsync<List<AgentDto>>(_jsonOptions);
        agentList.Should().NotBeNull();
        agentList.Should().HaveCount(2); // Only public agents
        agentList.Should().OnlyContain(a => a.IsPublic);
        agentList.Should().NotContain(a => a.Name == "Private Agent");
    }

    private static void SetTestUserId(HttpClient client, Guid userId)
    {
        client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
    }
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for test user ID header
        if (!Request.Headers.TryGetValue("X-Test-User-Id", out var userIdHeader)
            || string.IsNullOrEmpty(userIdHeader.FirstOrDefault()))
        {
            // No user ID provided - allow anonymous access (no authentication)
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
