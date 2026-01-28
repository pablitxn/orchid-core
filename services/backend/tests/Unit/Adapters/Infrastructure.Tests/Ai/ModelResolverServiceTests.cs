using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchid.Infrastructure.Ai.AgentFramework;
using Orchid.Infrastructure.Ai.AgentFramework.Services;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class ModelResolverServiceTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IMemoryCache> _memoryCacheMock;
    private readonly Mock<ILogger<ModelResolverService>> _loggerMock;
    private readonly IOptions<ModelResolutionSettings> _settings;
    private readonly ModelResolverService _service;

    public ModelResolverServiceTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _memoryCacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<ModelResolverService>>();

        _settings = Options.Create(new ModelResolutionSettings
        {
            DefaultModel = "gpt-5-nano-2025-08-07",
            EmergencyFallback = "gpt-5-nano-2025-08-07",
            UserPreferenceCacheMinutes = 5,
            EnableDetailedLogging = false
        });

        _service = new ModelResolverService(
            _agentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _memoryCacheMock.Object,
            _settings,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnRequestOverride_When_ValidOverrideProvided()
    {
        // Arrange
        var context = ModelResolutionContext.ForAgent(
            agentId: Guid.NewGuid(),
            requestOverride: "gpt-5-nano-2025-08-07");

        // Act
        var result = await _service.ResolveAsync(context);

        // Assert
        result.ModelId.Should().Be("gpt-5-nano-2025-08-07");
        result.Source.Should().Be(ModelResolutionSource.RequestOverride);
    }

    [Fact]
    public async Task ResolveAsync_Should_IgnoreInvalidOverride_And_FallToAgentModel()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            Model = "gpt-5-mini-2025-06-15"
        };

        _agentRepositoryMock
            .Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var context = ModelResolutionContext.ForAgent(
            agentId,
            requestOverride: "invalid-model");

        // Act
        var result = await _service.ResolveAsync(context);

        // Assert
        result.ModelId.Should().Be("gpt-5-mini-2025-06-15");
        result.Source.Should().Be(ModelResolutionSource.AgentConfiguration);
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnAgentModel_When_ConfiguredOnAgent()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            Model = "gpt-5-mini-2025-06-15"
        };

        _agentRepositoryMock
            .Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var context = ModelResolutionContext.ForAgent(agentId);

        // Act
        var result = await _service.ResolveAsync(context);

        // Assert
        result.ModelId.Should().Be("gpt-5-mini-2025-06-15");
        result.Source.Should().Be(ModelResolutionSource.AgentConfiguration);
    }

    [Fact]
    public async Task ResolveAsync_Should_UseAgentPreference_When_NoDirectModel()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            Model = null,
            ModelPreference = ModelPreference.Quality
        };

        _agentRepositoryMock
            .Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var context = ModelResolutionContext.ForAgent(agentId);

        // Act
        var result = await _service.ResolveAsync(context);

        // Assert
        result.ModelId.Should().Be("gpt-5-2025-06-15"); // Quality tier model
        result.Source.Should().Be(ModelResolutionSource.AgentPreference);
        result.AppliedPreference.Should().Be(ModelPreference.Quality);
    }

    [Fact]
    public async Task ResolveAsync_Should_UseFallbackModel_When_NoModelOrPreference()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            Model = null,
            ModelPreference = null,
            FallbackModel = "gpt-5-mini-2025-06-15"
        };

        _agentRepositoryMock
            .Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var context = ModelResolutionContext.ForAgent(agentId);

        // Act
        var result = await _service.ResolveAsync(context);

        // Assert
        result.ModelId.Should().Be("gpt-5-mini-2025-06-15");
        result.Source.Should().Be(ModelResolutionSource.AgentConfiguration);
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnSystemDefault_When_NoOtherConfigAvailable()
    {
        // Arrange
        var context = ModelResolutionContext.Default();

        // Act
        var result = await _service.ResolveAsync(context);

        // Assert
        result.ModelId.Should().Be("gpt-5-nano-2025-08-07");
        result.Source.Should().Be(ModelResolutionSource.SystemDefault);
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnEmergencyFallback_When_ExceptionOccurs()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _agentRepositoryMock
            .Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var context = ModelResolutionContext.ForAgent(agentId);

        // Act
        var result = await _service.ResolveAsync(context);

        // Assert
        result.ModelId.Should().Be("gpt-5-nano-2025-08-07");
        result.Source.Should().Be(ModelResolutionSource.EmergencyFallback);
    }

    [Fact]
    public async Task ResolveAsync_Should_UseUserPreference_When_AgentHasNoConfig()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cacheKey = $"model_pref:user:{userId}";

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            Model = null,
            ModelPreference = null,
            FallbackModel = null
        };

        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            ModelPreference = ModelPreference.Balanced
        };

        _agentRepositoryMock
            .Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        object? cachedValue = null;
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out cachedValue!))
            .Returns(false);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var cacheEntry = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        var context = ModelResolutionContext.ForAgent(agentId, userId);

        // Act
        var result = await _service.ResolveAsync(context);

        // Assert
        result.ModelId.Should().Be("gpt-5-mini-2025-06-15"); // Balanced tier model
        result.Source.Should().Be(ModelResolutionSource.UserPreference);
        result.AppliedPreference.Should().Be(ModelPreference.Balanced);
    }

    [Fact]
    public void IsValidModel_Should_ReturnTrue_ForKnownModels()
    {
        // Act & Assert
        _service.IsValidModel("gpt-5-nano-2025-08-07").Should().BeTrue();
        _service.IsValidModel("gpt-5-mini-2025-06-15").Should().BeTrue();
        _service.IsValidModel("gpt-5-2025-06-15").Should().BeTrue();
    }

    [Fact]
    public void IsValidModel_Should_ReturnFalse_ForUnknownModels()
    {
        // Act & Assert
        _service.IsValidModel("unknown-model").Should().BeFalse();
        _service.IsValidModel("gpt-3").Should().BeFalse();
        _service.IsValidModel("").Should().BeFalse();
    }

    [Fact]
    public void GetSystemDefault_Should_ReturnConfiguredDefault()
    {
        // Act
        var result = _service.GetSystemDefault();

        // Assert
        result.Should().Be("gpt-5-nano-2025-08-07");
    }

    [Fact]
    public void GetAvailableModels_Should_ReturnAllActiveModels()
    {
        // Act
        var models = _service.GetAvailableModels();

        // Assert
        models.Should().NotBeEmpty();
        models.Should().Contain(m => m.ModelId == "gpt-5-nano-2025-08-07");
        models.Should().Contain(m => m.ModelId == "gpt-5-mini-2025-06-15");
        models.Should().Contain(m => m.ModelId == "gpt-5-2025-06-15");
    }

    [Fact]
    public async Task ResolveAsync_Should_PreferAgentModel_Over_AgentPreference()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            Model = "gpt-5-nano-2025-08-07", // Specific model configured
            ModelPreference = ModelPreference.Quality // Also has preference set
        };

        _agentRepositoryMock
            .Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var context = ModelResolutionContext.ForAgent(agentId);

        // Act
        var result = await _service.ResolveAsync(context);

        // Assert
        result.ModelId.Should().Be("gpt-5-nano-2025-08-07"); // Should use direct model, not preference
        result.Source.Should().Be(ModelResolutionSource.AgentConfiguration);
    }
}
