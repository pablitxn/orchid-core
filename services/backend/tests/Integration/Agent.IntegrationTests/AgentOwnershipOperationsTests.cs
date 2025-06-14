using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.UseCases.Agent.Common;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Controllers;
using Xunit;

namespace Agent.IntegrationTests;

public class AgentOwnershipOperationsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AgentOwnershipOperationsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task CreateAgent_Should_Assign_Ownership_To_Creating_User()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
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

        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(userId)}");

        var createRequest = new CreateAgentRequest
        {
            Name = "My New Agent",
            Description = "Test agent",
            IsPublic = false,
            Language = "en"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents", createRequest);

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
        using var scope = _factory.Services.CreateScope();
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

        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(currentUserId)}");

        // Act
        var response = await _client.GetAsync("/api/agents");

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
        using var scope = _factory.Services.CreateScope();
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

        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(userId)}");

        // Act
        var response = await _client.DeleteAsync($"/api/agents/{agent.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // Verify soft delete in database
        var dbAgent = await context.Agents.FirstOrDefaultAsync(a => a.Id == agent.Id);
        dbAgent.Should().NotBeNull();
        dbAgent!.IsDeleted.Should().BeTrue();
        dbAgent.IsInRecycleBin.Should().BeTrue();
        dbAgent.DeletedAt.Should().NotBeNull();
        dbAgent.RecycleBinExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task DeleteAgent_Should_Fail_For_NonOwner()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
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

        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(otherUserId)}");

        // Act
        var response = await _client.DeleteAsync($"/api/agents/{agent.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        
        // Verify agent is still active in database
        var dbAgent = await context.Agents.FirstOrDefaultAsync(a => a.Id == agent.Id);
        dbAgent.Should().NotBeNull();
        dbAgent!.IsDeleted.Should().BeFalse();
        dbAgent.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateAgent_With_IsPublic_True_Should_Be_Accessible_To_Others()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
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
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(creatorId)}");

        var createRequest = new CreateAgentRequest
        {
            Name = "Public Test Agent",
            Description = "A public agent for testing",
            IsPublic = true,
            Language = "en"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/agents", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdAgent = await createResponse.Content.ReadFromJsonAsync<AgentDto>(_jsonOptions);

        // List agents as another user
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GenerateTestToken(otherId)}");

        // Act
        var listResponse = await _client.GetAsync("/api/agents");

        // Assert
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var agentList = await listResponse.Content.ReadFromJsonAsync<List<AgentDto>>(_jsonOptions);
        agentList.Should().Contain(a => a.Id == createdAgent!.Id && a.IsPublic);
    }

    [Fact]
    public async Task Unauthenticated_User_Can_See_Public_Agents_Only()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
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

        // No authentication header

        // Act
        var response = await _client.GetAsync("/api/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var agentList = await response.Content.ReadFromJsonAsync<List<AgentDto>>(_jsonOptions);
        agentList.Should().NotBeNull();
        agentList.Should().HaveCount(2); // Only public agents
        agentList.Should().OnlyContain(a => a.IsPublic);
        agentList.Should().NotContain(a => a.Name == "Private Agent");
    }

    private string GenerateTestToken(Guid userId)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId.ToString()));
    }
}