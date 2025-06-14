using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CreditSystem.IntegrationTests.Mocks;
using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CreditSystem.IntegrationTests;

public class ChatHubBasicTests : CreditSystemTestBase
{
    public ChatHubBasicTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task ChatHub_Should_Connect_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        
        // Act
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost/chathub?sessionId={sessionId}", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = async () => await GenerateTestToken(userId);
            })
            .Build();
        
        await connection.StartAsync();
        
        // Assert
        connection.State.Should().Be(HubConnectionState.Connected);
        
        await connection.DisposeAsync();
    }
    
    [Fact] 
    public async Task ChatHub_Should_Handle_Message_With_Valid_Session()
    {
        // Arrange
        await using var context = CreateTestContext();
        
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        var agentId = Guid.NewGuid();
        
        // Seed data
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            Name = "Test User"
        };
        
        var plan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = SubscriptionPlanEnum.Monthly100,
            Price = 29.99m,
            Credits = 100
        };
        
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100,
            SubscriptionPlanId = plan.Id
        };
        
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            IsPublic = false,
            UserId = userId
        };
        
        var chatSession = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow
        };
        
        await context.Users.AddAsync(user);
        await context.SubscriptionPlans.AddAsync(plan);
        await context.Subscriptions.AddAsync(subscription);
        await context.Agents.AddAsync(agent);
        await context.ChatSessions.AddAsync(chatSession);
        await context.SaveChangesAsync();
        
        // Add to mock repository
        using (var scope = Factory.Services.CreateScope())
        {
            var mockRepo = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>() as MockChatSessionRepository;
            mockRepo?.AddSession(chatSession);
        }
        
        // Act
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost/chathub?sessionId={sessionId}", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = async () => await GenerateTestToken(userId);
            })
            .Build();
        
        var messages = new List<(string user, string message)>();
        var errors = new List<string>();
        
        connection.On<string, string>("ReceiveMessage", (user, message) =>
        {
            messages.Add((user, message));
            if (user == "bot")
            {
                errors.Add(message);
            }
        });
        
        await connection.StartAsync();
        
        // Send message
        try
        {
            await connection.InvokeAsync("SendMessage", sessionId, "Test message", false);
            await Task.Delay(1000); // Wait for processing
        }
        catch (HubException ex)
        {
            // Log more details about the error
            throw new Exception($"Hub error: {ex.Message}. Messages received: {string.Join(", ", messages.Select(m => $"{m.user}: {m.message}"))}. Errors: {string.Join(", ", errors)}", ex);
        }
        
        // Assert
        messages.Should().NotBeEmpty();
        messages.Should().Contain(m => m.message.Contains("Mock AI response"));
        
        await connection.DisposeAsync();
    }
}