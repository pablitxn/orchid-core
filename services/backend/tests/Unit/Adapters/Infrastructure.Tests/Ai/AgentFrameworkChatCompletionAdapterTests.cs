using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AppChatMessage = Application.Interfaces.ChatMessage;

namespace Infrastructure.Tests.Ai;

public class AgentFrameworkChatCompletionAdapterTests
{
    private readonly Mock<IChatClient> _chatClientMock;
    private readonly Mock<IAgentFactory> _agentFactoryMock;
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IEnhancedTelemetryClient> _telemetryMock;
    private readonly Mock<ILogger<AgentFrameworkChatCompletionAdapter>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IAgentToolLoader> _toolLoaderMock;
    private readonly AgentFrameworkChatCompletionAdapter _adapter;

    /// <summary>
    /// Creates a real ChatResponse with the given text (since ChatResponse.Text is non-virtual).
    /// </summary>
    private static ChatResponse CreateChatResponse(string text)
    {
        return new ChatResponse(new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.Assistant, text)
        });
    }

    public AgentFrameworkChatCompletionAdapterTests()
    {
        _chatClientMock = new Mock<IChatClient>();
        _agentFactoryMock = new Mock<IAgentFactory>();
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _telemetryMock = new Mock<IEnhancedTelemetryClient>();
        _loggerMock = new Mock<ILogger<AgentFrameworkChatCompletionAdapter>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _toolLoaderMock = new Mock<IAgentToolLoader>();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IAgentToolLoader)))
            .Returns(_toolLoaderMock.Object);

        _adapter = new AgentFrameworkChatCompletionAdapter(
            _chatClientMock.Object,
            _agentFactoryMock.Object,
            _agentRepositoryMock.Object,
            _serviceProviderMock.Object,
            _telemetryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CompleteAsync_Should_ReturnResponse_When_ChatClientSucceeds()
    {
        // Arrange
        var messages = new List<AppChatMessage>
        {
            new("system", "You are a helpful assistant."),
            new("user", "Hello")
        };

        var chatResponse = CreateChatResponse("Hello! How can I help you?");

        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act
        var result = await _adapter.CompleteAsync(messages, CancellationToken.None);

        // Assert
        result.Should().Be("Hello! How can I help you?");
        _chatClientMock.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_ThrowException_When_AgentNotFound()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var messages = new List<AppChatMessage>
        {
            new("user", "Hello")
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        // Act & Assert
        var act = () => _adapter.CompleteWithAgentAsync(messages, agentId, null, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Agent with ID {agentId} not found.");
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_UseDefaultModel_When_AgentHasNoModel()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var messages = new List<AppChatMessage>
        {
            new("system", "You are a helpful assistant."),
            new("user", "Hello")
        };

        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            Model = null,
            Personality = "You are friendly"
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentEntity);

        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AIFunction>());

        var mockChatClient = new Mock<IChatClient>();
        var chatResponse = CreateChatResponse("Agent response");

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        // Default model is used when agent has no model
        _agentFactoryMock
            .Setup(f => f.GetChatClient(It.Is<string>(m => m == "gpt-5-nano-2025-08-07")))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act
        var result = await _adapter.CompleteWithAgentAsync(messages, agentId, userId, CancellationToken.None);

        // Assert
        result.Should().Be("Agent response");

        // Verify GetChatClient was called with default model
        _agentFactoryMock.Verify(f => f.GetChatClient("gpt-5-nano-2025-08-07"), Times.Once);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_UseAgentModel_When_Specified()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var customModel = "gpt-4o-mini";
        var messages = new List<AppChatMessage>
        {
            new("system", "You are a helpful assistant."),
            new("user", "Hello")
        };

        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = "Custom Model Agent",
            Model = customModel,
            Personality = "You are professional"
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentEntity);

        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AIFunction>());

        var mockChatClient = new Mock<IChatClient>();
        var chatResponse = CreateChatResponse("Custom model response");

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        _agentFactoryMock
            .Setup(f => f.GetChatClient(customModel))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act
        var result = await _adapter.CompleteWithAgentAsync(messages, agentId, userId, CancellationToken.None);

        // Assert
        result.Should().Be("Custom model response");

        // Verify GetChatClient was called with custom model
        _agentFactoryMock.Verify(f => f.GetChatClient(customModel), Times.Once);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_LoadAgentTools()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var messages = new List<AppChatMessage>
        {
            new("user", "Search for information")
        };

        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = "Tool Agent",
            Model = null
        };

        var mockTools = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "search result", "SearchWeb"),
            AIFunctionFactory.Create(() => "stored", "StoreDocument")
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentEntity);

        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTools);

        var mockChatClient = new Mock<IChatClient>();
        var chatResponse = CreateChatResponse("Response with tools");

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        _agentFactoryMock
            .Setup(f => f.GetChatClient(It.IsAny<string>()))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act
        await _adapter.CompleteWithAgentAsync(messages, agentId, null, CancellationToken.None);

        // Assert
        _toolLoaderMock.Verify(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()), Times.Once);

        // Verify chat options include tools
        mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
            It.Is<ChatOptions>(opts => opts.Tools != null && opts.Tools.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_UseAgentPersonality_When_NoSystemMessage()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var messages = new List<AppChatMessage>
        {
            new("user", "Hello") // No system message
        };

        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = "Personality Agent",
            Model = null,
            Personality = "You are a pirate assistant who speaks like a sailor."
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentEntity);

        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AIFunction>());

        var mockChatClient = new Mock<IChatClient>();
        var chatResponse = CreateChatResponse("Ahoy!");

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        _agentFactoryMock
            .Setup(f => f.GetChatClient(It.IsAny<string>()))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act
        var result = await _adapter.CompleteWithAgentAsync(messages, agentId, null, CancellationToken.None);

        // Assert
        result.Should().Be("Ahoy!");

        // Verify the personality was added as system message
        mockChatClient.Verify(c => c.GetResponseAsync(
            It.Is<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(msgs =>
                msgs.Any(m => m.Role == ChatRole.System && m.Text == agentEntity.Personality)),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_RecordTelemetry_WithAgentMetadata()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = "test-session-123";
        var messages = new List<AppChatMessage>
        {
            new("user", "Hello", Metadata: new Dictionary<string, object> { ["sessionId"] = sessionId })
        };

        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = "Telemetry Test Agent",
            Model = "custom-model"
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentEntity);

        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AIFunction>());

        var mockChatClient = new Mock<IChatClient>();
        var chatResponse = CreateChatResponse("Response");

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        _agentFactoryMock
            .Setup(f => f.GetChatClient(It.IsAny<string>()))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                sessionId,
                userId.ToString(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act
        await _adapter.CompleteWithAgentAsync(messages, agentId, userId, CancellationToken.None);

        // Assert
        _telemetryMock.Verify(t => t.StartTraceAsync(
            It.Is<string>(s => s.Contains(agentId.ToString())),
            sessionId,
            userId.ToString(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _telemetryMock.Verify(t => t.RecordGenerationAsync(
            "trace-123",
            agentEntity.Model,
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        _telemetryMock.Verify(t => t.EndTraceAsync(
            "trace-123",
            true,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_RecordErrorTelemetry_OnException()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var messages = new List<AppChatMessage>
        {
            new("user", "Hello", Metadata: new Dictionary<string, object> { ["sessionId"] = "session-123" })
        };

        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = "Error Test Agent",
            Model = null
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentEntity);

        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AIFunction>());

        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        _agentFactoryMock
            .Setup(f => f.GetChatClient(It.IsAny<string>()))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _adapter.CompleteWithAgentAsync(messages, agentId, null, CancellationToken.None));

        _telemetryMock.Verify(t => t.RecordEventAsync(
            "trace-123",
            "error",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _telemetryMock.Verify(t => t.EndTraceAsync(
            "trace-123",
            false,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ChatClientIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AgentFrameworkChatCompletionAdapter(
            null!,
            _agentFactoryMock.Object,
            _agentRepositoryMock.Object,
            _serviceProviderMock.Object,
            _telemetryMock.Object,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_AgentFactoryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AgentFrameworkChatCompletionAdapter(
            _chatClientMock.Object,
            null!,
            _agentRepositoryMock.Object,
            _serviceProviderMock.Object,
            _telemetryMock.Object,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_AgentRepositoryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AgentFrameworkChatCompletionAdapter(
            _chatClientMock.Object,
            _agentFactoryMock.Object,
            null!,
            _serviceProviderMock.Object,
            _telemetryMock.Object,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ServiceProviderIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AgentFrameworkChatCompletionAdapter(
            _chatClientMock.Object,
            _agentFactoryMock.Object,
            _agentRepositoryMock.Object,
            null!,
            _telemetryMock.Object,
            _loggerMock.Object));
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_WorkWithNoTools()
    {
        // Arrange - Agent with no tools configured
        var agentId = Guid.NewGuid();
        var messages = new List<AppChatMessage>
        {
            new("user", "Hello, simple chat")
        };

        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = "Simple Agent",
            Model = null
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentEntity);

        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AIFunction>());

        var mockChatClient = new Mock<IChatClient>();
        var chatResponse = CreateChatResponse("Hello! I'm a simple assistant.");

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        _agentFactoryMock
            .Setup(f => f.GetChatClient(It.IsAny<string>()))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act
        var result = await _adapter.CompleteWithAgentAsync(messages, agentId, null, CancellationToken.None);

        // Assert
        result.Should().Be("Hello! I'm a simple assistant.");
        _toolLoaderMock.Verify(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()), Times.Once);

        // Verify chat options have empty tools list
        mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
            It.Is<ChatOptions>(opts => opts.Tools == null || opts.Tools.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_WorkWithoutToolLoader()
    {
        // Arrange - Tool loader not registered in DI
        var serviceProviderNoTools = new Mock<IServiceProvider>();
        serviceProviderNoTools
            .Setup(sp => sp.GetService(typeof(IAgentToolLoader)))
            .Returns(null);

        var adapterNoTools = new AgentFrameworkChatCompletionAdapter(
            _chatClientMock.Object,
            _agentFactoryMock.Object,
            _agentRepositoryMock.Object,
            serviceProviderNoTools.Object,
            _telemetryMock.Object,
            _loggerMock.Object);

        var agentId = Guid.NewGuid();
        var messages = new List<AppChatMessage>
        {
            new("user", "Hello")
        };

        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = "Fallback Agent",
            Model = null
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentEntity);

        var mockChatClient = new Mock<IChatClient>();
        var chatResponse = CreateChatResponse("Response without tools");

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        _agentFactoryMock
            .Setup(f => f.GetChatClient(It.IsAny<string>()))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act
        var result = await adapterNoTools.CompleteWithAgentAsync(messages, agentId, null, CancellationToken.None);

        // Assert
        result.Should().Be("Response without tools");

        // Verify chat options have empty tools list
        mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
            It.Is<ChatOptions>(opts => opts.Tools == null || opts.Tools.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_PassToolsToTelemetry()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var messages = new List<AppChatMessage>
        {
            new("user", "Use tools", Metadata: new Dictionary<string, object> { ["sessionId"] = "session-tools" })
        };

        var agentEntity = new AgentEntity
        {
            Id = agentId,
            Name = "Telemetry Tool Agent",
            Model = null
        };

        var mockTools = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "result", "Tool1"),
            AIFunctionFactory.Create(() => "result", "Tool2")
        };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentEntity);

        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTools);

        var mockChatClient = new Mock<IChatClient>();
        var chatResponse = CreateChatResponse("Response");

        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        _agentFactoryMock
            .Setup(f => f.GetChatClient(It.IsAny<string>()))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        // Act
        await _adapter.CompleteWithAgentAsync(messages, agentId, null, CancellationToken.None);

        // Assert - Verify telemetry includes tool information
        _telemetryMock.Verify(t => t.RecordGenerationAsync(
            "trace-123",
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.Is<object>(meta => meta != null),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_Should_OnlyLoadToolsForSpecificAgent()
    {
        // Arrange - Two different agents
        var agent1Id = Guid.NewGuid();
        var agent2Id = Guid.NewGuid();

        var agent1 = new AgentEntity { Id = agent1Id, Name = "Agent1" };
        var agent2 = new AgentEntity { Id = agent2Id, Name = "Agent2" };

        var agent1Tools = new List<AIFunction> { AIFunctionFactory.Create(() => "result", "Agent1Tool") };
        var agent2Tools = new List<AIFunction> { AIFunctionFactory.Create(() => "result", "Agent2Tool") };

        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agent1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent1);
        _agentRepositoryMock
            .Setup(r => r.GetByIdAsync(agent2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent2);

        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agent1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent1Tools);
        _toolLoaderMock
            .Setup(t => t.LoadAgentToolsAsync(agent2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent2Tools);

        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateChatResponse("Response"));

        _agentFactoryMock
            .Setup(f => f.GetChatClient(It.IsAny<string>()))
            .Returns(mockChatClient.Object);

        _telemetryMock
            .Setup(t => t.StartTraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        var messages = new List<AppChatMessage> { new("user", "Test") };

        // Act - Call with agent1
        await _adapter.CompleteWithAgentAsync(messages, agent1Id, null, CancellationToken.None);

        // Assert - Only agent1 tools were loaded
        _toolLoaderMock.Verify(t => t.LoadAgentToolsAsync(agent1Id, It.IsAny<CancellationToken>()), Times.Once);
        _toolLoaderMock.Verify(t => t.LoadAgentToolsAsync(agent2Id, It.IsAny<CancellationToken>()), Times.Never);

        // Verify agent1 tool was passed
        mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(),
            It.Is<ChatOptions>(opts => opts.Tools != null && opts.Tools.Any(t => t.Name == "Agent1Tool")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
