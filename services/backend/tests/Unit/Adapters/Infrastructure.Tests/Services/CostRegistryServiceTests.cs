using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WebApi.Configuration;
using Xunit;
using System;

namespace Infrastructure.Tests.Services;

public class CostRegistryServiceTests
{
    private readonly Mock<ICostConfigurationRepository> _costConfigRepositoryMock;
    private readonly Mock<IMemoryCache> _memoryCacheMock;
    private readonly Mock<ILogger<CostRegistryService>> _loggerMock;
    private readonly IOptions<CreditSystemConfiguration> _creditConfig;
    private readonly CostRegistryService _service;

    public CostRegistryServiceTests()
    {
        _costConfigRepositoryMock = new Mock<ICostConfigurationRepository>();
        _memoryCacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<CostRegistryService>>();
        
        _creditConfig = Options.Create(new CreditSystemConfiguration
        {
            MinimumCreditsPerMessage = 5,
            TokensPerCredit = 1000
        });

        _service = new CostRegistryService(
            _costConfigRepositoryMock.Object,
            _memoryCacheMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task GetMessageFixedCost_Should_ReturnCachedValue_When_Available()
    {
        // Arrange
        var cacheKey = "cost_config:message_fixed:default";
        var cachedConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "message_fixed",
            CreditCost = 8,
            IsActive = true
        };
        object cachedValue = cachedConfig;
        
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out cachedValue))
            .Returns(true);

        // Act
        var result = await _service.GetMessageFixedCostAsync(CancellationToken.None);

        // Assert
        result.Should().Be(8);
        _costConfigRepositoryMock.Verify(x => x.GetCostForTypeAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMessageFixedCost_Should_QueryDatabase_When_NotCached()
    {
        // Arrange
        var cacheKey = "cost_config:message_fixed:default";
        object nullValue = null!;
        var costConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "message_fixed",
            CreditCost = 7,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1)
        };
        
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);
            
        _costConfigRepositoryMock
            .Setup(x => x.GetCostForTypeAsync("message_fixed", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(costConfig);
            
        var cacheEntry = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.GetMessageFixedCostAsync(CancellationToken.None);

        // Assert
        result.Should().Be(7);
        _costConfigRepositoryMock.Verify(x => x.GetCostForTypeAsync("message_fixed", null, It.IsAny<CancellationToken>()), Times.Once);
        _memoryCacheMock.Verify(x => x.CreateEntry(cacheKey), Times.Once);
    }

    [Fact]
    public async Task GetMessageFixedCost_Should_ReturnDefault_When_NotConfigured()
    {
        // Arrange
        var cacheKey = "cost_config:message_fixed:default";
        object nullValue = null!;
        
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);
            
        _costConfigRepositoryMock
            .Setup(x => x.GetCostForTypeAsync("message_fixed", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CostConfigurationEntity?)null);

        // Act
        var result = await _service.GetMessageFixedCostAsync(CancellationToken.None);

        // Assert
        result.Should().Be(5); // Default from constant
        _memoryCacheMock.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task GetMessageTokenCostPer1k_Should_ReturnTokenBasedCost_When_Configured()
    {
        // Arrange
        var cacheKey = "cost_config:message_token:default";
        object nullValue = null!;
        var costConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "message_token",
            CostPer1kTokens = 2.5m,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1)
        };
        
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);
            
        _costConfigRepositoryMock
            .Setup(x => x.GetCostForTypeAsync("message_token", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(costConfig);
            
        var cacheEntry = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.GetMessageTokenCostPer1kAsync(CancellationToken.None);

        // Assert
        result.Should().Be(2.5m);
    }

    [Fact]
    public async Task GetPluginUsageCost_Should_ReturnSpecificCost_When_Configured()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var cacheKey = $"cost_config:plugin_usage:{pluginId}";
        object nullValue = null!;
        var costConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "plugin_usage",
            ResourceId = pluginId,
            CreditCost = 15,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1)
        };
        
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);
            
        _costConfigRepositoryMock
            .Setup(x => x.GetCostForTypeAsync("plugin_usage", pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(costConfig);
            
        var cacheEntry = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.GetPluginUsageCostAsync(pluginId, CancellationToken.None);

        // Assert
        result.Should().Be(15);
    }

    [Fact]
    public async Task GetPluginPurchaseCost_Should_ReturnSpecificCost_When_Configured()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var cacheKey = $"cost_config:plugin_purchase:{pluginId}";
        object nullValue = null!;
        var costConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "plugin_purchase",
            ResourceId = pluginId,
            CreditCost = 75,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1)
        };
        
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);
            
        _costConfigRepositoryMock
            .Setup(x => x.GetCostForTypeAsync("plugin_purchase", pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(costConfig);
            
        var cacheEntry = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.GetPluginPurchaseCostAsync(pluginId, CancellationToken.None);

        // Assert
        result.Should().Be(75);
    }

    [Fact]
    public async Task GetWorkflowUsageCost_Should_ReturnDefaultCost_When_NotConfigured()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var cacheKey = $"cost_config:workflow_usage:{workflowId}";
        object nullValue = null!;
        
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);
            
        _costConfigRepositoryMock
            .Setup(x => x.GetCostForTypeAsync("workflow_usage", workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CostConfigurationEntity?)null);
            
        var cacheEntry = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.GetWorkflowUsageCostAsync(workflowId, CancellationToken.None);

        // Assert
        result.Should().Be(20); // Default from configuration
    }

    [Fact]
    public async Task GetWorkflowPurchaseCost_Should_ReturnSpecificCost_When_Configured()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var cacheKey = $"cost_config:workflow_purchase:{workflowId}";
        object nullValue = null!;
        var costConfig = new CostConfigurationEntity
        {
            Id = Guid.NewGuid(),
            CostType = "workflow_purchase",
            ResourceId = workflowId,
            CreditCost = 150,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1)
        };
        
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);
            
        _costConfigRepositoryMock
            .Setup(x => x.GetCostForTypeAsync("workflow_purchase", workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(costConfig);
            
        var cacheEntry = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.GetWorkflowPurchaseCostAsync(workflowId, CancellationToken.None);

        // Assert
        result.Should().Be(150);
    }

    // Note: InvalidateCacheAsync method is not part of the ICostRegistry interface
    // This test should be removed or the interface should be updated


    // Note: SetCostAsync method is not part of the ICostRegistry interface
    // This test should be removed or the interface should be updated
}