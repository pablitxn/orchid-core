using Application.Interfaces;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Middleware;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class TelemetryContextTests
{
    [Fact]
    public void BeginScope_Should_SetTraceId()
    {
        // Arrange
        var traceId = "test-trace-123";

        // Act
        using (TelemetryContext.BeginScope(traceId))
        {
            // Assert
            TelemetryContext.TraceId.Should().Be(traceId);
            TelemetryContext.IsEnabled.Should().BeTrue();
        }

        // After scope disposed
        TelemetryContext.TraceId.Should().BeNull();
        TelemetryContext.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void BeginScope_Should_SetSessionIdAndUserId()
    {
        // Arrange
        var traceId = "test-trace-123";
        var sessionId = "session-456";
        var userId = "user-789";

        // Act & Assert
        using (TelemetryContext.BeginScope(traceId, sessionId, userId))
        {
            TelemetryContext.TraceId.Should().Be(traceId);
            TelemetryContext.SessionId.Should().Be(sessionId);
            TelemetryContext.UserId.Should().Be(userId);
            TelemetryContext.IsEnabled.Should().BeTrue();
        }
    }

    [Fact]
    public void BeginDisabledScope_Should_DisableTelemetry()
    {
        // Act
        using (TelemetryContext.BeginDisabledScope())
        {
            // Assert
            TelemetryContext.IsEnabled.Should().BeFalse();
            TelemetryContext.TraceId.Should().BeNull();
        }
    }

    [Fact]
    public void NestedScopes_Should_RestorePreviousScope()
    {
        // Arrange
        var outerTraceId = "outer-trace";
        var innerTraceId = "inner-trace";

        // Act & Assert
        using (TelemetryContext.BeginScope(outerTraceId))
        {
            TelemetryContext.TraceId.Should().Be(outerTraceId);

            using (TelemetryContext.BeginScope(innerTraceId))
            {
                TelemetryContext.TraceId.Should().Be(innerTraceId);
            }

            // After inner scope disposed, should restore outer
            TelemetryContext.TraceId.Should().Be(outerTraceId);
        }

        // After all scopes disposed
        TelemetryContext.TraceId.Should().BeNull();
    }

    [Fact]
    public async Task AsyncLocal_Should_FlowAcrossAsyncBoundaries()
    {
        // Arrange
        var traceId = "async-trace-123";
        string? capturedTraceId = null;

        // Act
        using (TelemetryContext.BeginScope(traceId))
        {
            await Task.Run(() =>
            {
                capturedTraceId = TelemetryContext.TraceId;
            });
        }

        // Assert
        capturedTraceId.Should().Be(traceId);
    }
}

public class TelemetryAIFunctionWrapperTests
{
    private readonly Mock<IEnhancedTelemetryClient> _telemetryClientMock;
    private readonly Mock<ILogger<TelemetryAIFunctionWrapperTests>> _loggerMock;

    public TelemetryAIFunctionWrapperTests()
    {
        _telemetryClientMock = new Mock<IEnhancedTelemetryClient>();
        _loggerMock = new Mock<ILogger<TelemetryAIFunctionWrapperTests>>();
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnResult_When_FunctionSucceeds()
    {
        // Arrange
        var expectedResult = "success result";
        var innerFunction = AIFunctionFactory.Create(() => expectedResult, "TestFunction");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act
        using (TelemetryContext.BeginScope("test-trace"))
        {
            var result = await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

            // Assert
            result?.ToString().Should().Be(expectedResult);
        }
    }

    [Fact]
    public async Task InvokeAsync_Should_RecordFunctionInvokingEvent_BeforeExecution()
    {
        // Arrange
        var traceId = "test-trace-123";
        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunction");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act
        using (TelemetryContext.BeginScope(traceId))
        {
            await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        }

        // Assert
        _telemetryClientMock.Verify(
            x => x.RecordEventAsync(
                traceId,
                "function_invoking",
                It.Is<object>(o => o.ToString()!.Contains("TestFunction")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_RecordFunctionInvokedEvent_AfterSuccessfulExecution()
    {
        // Arrange
        var traceId = "test-trace-123";
        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunction");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act
        using (TelemetryContext.BeginScope(traceId))
        {
            await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        }

        // Assert
        _telemetryClientMock.Verify(
            x => x.RecordEventAsync(
                traceId,
                "function_invoked",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_RecordToolInvocation_AfterSuccessfulExecution()
    {
        // Arrange
        var traceId = "test-trace-123";
        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunction");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act
        using (TelemetryContext.BeginScope(traceId))
        {
            await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        }

        // Assert
        _telemetryClientMock.Verify(
            x => x.RecordToolInvocationAsync(
                traceId,
                "TestFunction",
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_RecordFunctionErrorEvent_WhenExceptionThrown()
    {
        // Arrange
        var traceId = "test-trace-123";
        var innerFunction = AIFunctionFactory.Create(
            () => ThrowInvalidOperation("Test error"),
            "FailingFunction");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act & Assert
        using (TelemetryContext.BeginScope(traceId))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None).AsTask());
        }

        _telemetryClientMock.Verify(
            x => x.RecordEventAsync(
                traceId,
                "function_error",
                It.Is<object>(o => o.ToString()!.Contains("Test error")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static string ThrowInvalidOperation(string message)
    {
        throw new InvalidOperationException(message);
    }

    private static string ThrowOperationCanceled(string message)
    {
        throw new OperationCanceledException(message);
    }

    [Fact]
    public async Task InvokeAsync_Should_SkipTelemetry_WhenNoTraceContext()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunction");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act - No TelemetryContext.BeginScope()
        var result = await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert
        result?.ToString().Should().Be("result");
        _telemetryClientMock.Verify(
            x => x.RecordEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_Should_SkipTelemetry_WhenDisabledScope()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunction");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act
        using (TelemetryContext.BeginDisabledScope())
        {
            var result = await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

            // Assert
            result?.ToString().Should().Be("result");
        }

        _telemetryClientMock.Verify(
            x => x.RecordEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_Should_PropagateOperationCanceledException()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(
            () => ThrowOperationCanceled("Cancelled"),
            "CancelledFunction");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act & Assert
        using (TelemetryContext.BeginScope("test-trace"))
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None).AsTask());
        }

        // Should not record error event for cancellation
        _telemetryClientMock.Verify(
            x => x.RecordEventAsync(
                It.IsAny<string>(),
                "function_error",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Name_Should_ReturnInnerFunctionName()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(() => "result", "MyFunctionName");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Assert
        wrapper.Name.Should().Be("MyFunctionName");
    }

    [Fact]
    public void Description_Should_ReturnInnerFunctionDescription()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(
            () => "result",
            "TestFunc",
            "A test function description");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Assert
        wrapper.Description.Should().Be("A test function description");
    }

    [Fact]
    public void WrapAll_Should_WrapAllFunctions()
    {
        // Arrange
        var functions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "result1", "Func1"),
            AIFunctionFactory.Create(() => "result2", "Func2"),
            AIFunctionFactory.Create(() => "result3", "Func3")
        };

        // Act
        var wrapped = TelemetryAIFunctionWrapper.WrapAll(functions, _telemetryClientMock.Object, _loggerMock.Object);

        // Assert
        wrapped.Should().HaveCount(3);
        wrapped.Should().AllSatisfy(f => f.Should().BeOfType<TelemetryAIFunctionWrapper>());
        wrapped.Select(f => f.Name).Should().BeEquivalentTo(new[] { "Func1", "Func2", "Func3" });
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_InnerFunctionIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TelemetryAIFunctionWrapper(null!, _telemetryClientMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_TelemetryClientIsNull()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunc");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TelemetryAIFunctionWrapper(innerFunction, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunc");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, null!));
    }

    [Fact]
    public async Task InvokeAsync_Should_ContinueExecution_WhenTelemetryFails()
    {
        // Arrange
        var traceId = "test-trace-123";
        var innerFunction = AIFunctionFactory.Create(() => "success", "TestFunction");

        _telemetryClientMock
            .Setup(x => x.RecordEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Telemetry failure"));

        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act
        using (TelemetryContext.BeginScope(traceId))
        {
            var result = await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

            // Assert - Function should still succeed even if telemetry fails
            result?.ToString().Should().Be("success");
        }
    }

    [Fact]
    public async Task InvokeAsync_Should_IncludeSessionIdInEvents()
    {
        // Arrange
        var traceId = "test-trace-123";
        var sessionId = "session-456";
        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunction");
        var wrapper = new TelemetryAIFunctionWrapper(innerFunction, _telemetryClientMock.Object, _loggerMock.Object);

        // Act
        using (TelemetryContext.BeginScope(traceId, sessionId))
        {
            await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        }

        // Assert
        _telemetryClientMock.Verify(
            x => x.RecordEventAsync(
                traceId,
                "function_invoking",
                It.Is<object>(o => o.ToString()!.Contains(sessionId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class CreditTrackingTests
{
    private readonly Mock<IEnhancedTelemetryClient> _telemetryClientMock;
    private readonly Mock<ICreditTrackingService> _creditTrackingServiceMock;
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<ILogger<CreditTrackingTests>> _loggerMock;

    public CreditTrackingTests()
    {
        _telemetryClientMock = new Mock<IEnhancedTelemetryClient>();
        _creditTrackingServiceMock = new Mock<ICreditTrackingService>();
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _loggerMock = new Mock<ILogger<CreditTrackingTests>>();
    }

    [Fact]
    public async Task InvokeAsync_Should_TrackCredits_WhenSuccessful()
    {
        // Arrange
        var traceId = "test-trace-123";
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var toolSystemName = "web_search";

        var plugin = new Domain.Entities.PluginEntity
        {
            Id = pluginId,
            Name = "Web Search",
            SystemName = toolSystemName,
            PriceCredits = 25,
            IsActive = true
        };

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync(toolSystemName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _creditTrackingServiceMock
            .Setup(x => x.TrackPluginUsageAsync(
                userId,
                pluginId,
                plugin.Name,
                plugin.PriceCredits,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.CreditConsumptionEntity());

        var innerFunction = AIFunctionFactory.Create(() => "result", "SearchWeb");
        var wrapper = new TelemetryAIFunctionWrapper(
            innerFunction,
            _telemetryClientMock.Object,
            _loggerMock.Object,
            _creditTrackingServiceMock.Object,
            _pluginRepositoryMock.Object,
            null,
            toolSystemName);

        // Act
        using (TelemetryContext.BeginScope(traceId, "session-123", userId.ToString()))
        {
            await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        }

        // Assert
        _creditTrackingServiceMock.Verify(
            x => x.TrackPluginUsageAsync(
                userId,
                pluginId,
                plugin.Name,
                plugin.PriceCredits,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_NotTrackCredits_WhenNoUserId()
    {
        // Arrange
        var traceId = "test-trace-123";
        var toolSystemName = "web_search";

        var innerFunction = AIFunctionFactory.Create(() => "result", "SearchWeb");
        var wrapper = new TelemetryAIFunctionWrapper(
            innerFunction,
            _telemetryClientMock.Object,
            _loggerMock.Object,
            _creditTrackingServiceMock.Object,
            _pluginRepositoryMock.Object,
            null,
            toolSystemName);

        // Act - No userId in scope
        using (TelemetryContext.BeginScope(traceId, "session-123"))
        {
            await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        }

        // Assert
        _creditTrackingServiceMock.Verify(
            x => x.TrackPluginUsageAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_Should_NotTrackCredits_WhenPluginNotFound()
    {
        // Arrange
        var traceId = "test-trace-123";
        var userId = Guid.NewGuid();
        var toolSystemName = "unknown_tool";

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync(toolSystemName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.PluginEntity?)null);

        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunction");
        var wrapper = new TelemetryAIFunctionWrapper(
            innerFunction,
            _telemetryClientMock.Object,
            _loggerMock.Object,
            _creditTrackingServiceMock.Object,
            _pluginRepositoryMock.Object,
            null,
            toolSystemName);

        // Act
        using (TelemetryContext.BeginScope(traceId, "session-123", userId.ToString()))
        {
            await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        }

        // Assert
        _creditTrackingServiceMock.Verify(
            x => x.TrackPluginUsageAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static string ThrowInvalidOperationForCredit(string message)
    {
        throw new InvalidOperationException(message);
    }

    [Fact]
    public async Task InvokeAsync_Should_NotTrackCredits_WhenFunctionFails()
    {
        // Arrange
        var traceId = "test-trace-123";
        var userId = Guid.NewGuid();
        var toolSystemName = "web_search";

        var innerFunction = AIFunctionFactory.Create(
            () => ThrowInvalidOperationForCredit("Function failed"),
            "SearchWeb");

        var wrapper = new TelemetryAIFunctionWrapper(
            innerFunction,
            _telemetryClientMock.Object,
            _loggerMock.Object,
            _creditTrackingServiceMock.Object,
            _pluginRepositoryMock.Object,
            null,
            toolSystemName);

        // Act & Assert
        using (TelemetryContext.BeginScope(traceId, "session-123", userId.ToString()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None).AsTask());
        }

        // Credit tracking should not be called when function fails
        _creditTrackingServiceMock.Verify(
            x => x.TrackPluginUsageAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_Should_ContinueExecution_WhenCreditTrackingFails()
    {
        // Arrange
        var traceId = "test-trace-123";
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var toolSystemName = "web_search";

        var plugin = new Domain.Entities.PluginEntity
        {
            Id = pluginId,
            Name = "Web Search",
            SystemName = toolSystemName,
            PriceCredits = 25,
            IsActive = true
        };

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync(toolSystemName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _creditTrackingServiceMock
            .Setup(x => x.TrackPluginUsageAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Credit tracking failure"));

        var innerFunction = AIFunctionFactory.Create(() => "success", "SearchWeb");
        var wrapper = new TelemetryAIFunctionWrapper(
            innerFunction,
            _telemetryClientMock.Object,
            _loggerMock.Object,
            _creditTrackingServiceMock.Object,
            _pluginRepositoryMock.Object,
            null,
            toolSystemName);

        // Act
        using (TelemetryContext.BeginScope(traceId, "session-123", userId.ToString()))
        {
            var result = await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

            // Assert - Function should still succeed even if credit tracking fails
            result?.ToString().Should().Be("success");
        }
    }

    [Fact]
    public async Task InvokeAsync_Should_UseContextMapping_WhenNoExplicitToolName()
    {
        // Arrange
        var traceId = "test-trace-123";
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var toolSystemName = "vector_store";
        var functionName = "SearchDocuments";

        var plugin = new Domain.Entities.PluginEntity
        {
            Id = pluginId,
            Name = "Vector Store",
            SystemName = toolSystemName,
            PriceCredits = 15,
            IsActive = true
        };

        _pluginRepositoryMock
            .Setup(x => x.GetBySystemNameAsync(toolSystemName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        _creditTrackingServiceMock
            .Setup(x => x.TrackPluginUsageAsync(
                userId,
                pluginId,
                plugin.Name,
                plugin.PriceCredits,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.CreditConsumptionEntity());

        var innerFunction = AIFunctionFactory.Create(() => "result", functionName);
        // No explicit toolSystemName - should use context mapping
        var wrapper = new TelemetryAIFunctionWrapper(
            innerFunction,
            _telemetryClientMock.Object,
            _loggerMock.Object,
            _creditTrackingServiceMock.Object,
            _pluginRepositoryMock.Object,
            toolSystemName: null);

        // Create function-to-tool mapping
        var functionToolMapping = new Dictionary<string, string>
        {
            [functionName] = toolSystemName
        };

        // Act
        using (TelemetryContext.BeginScope(traceId, "session-123", userId.ToString(), null, functionToolMapping))
        {
            await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        }

        // Assert
        _creditTrackingServiceMock.Verify(
            x => x.TrackPluginUsageAsync(
                userId,
                pluginId,
                plugin.Name,
                plugin.PriceCredits,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void WrapAllWithCreditTracking_Should_WrapAllFunctions()
    {
        // Arrange
        var functions = new List<AIFunction>
        {
            AIFunctionFactory.Create(() => "result1", "Func1"),
            AIFunctionFactory.Create(() => "result2", "Func2"),
            AIFunctionFactory.Create(() => "result3", "Func3")
        };

        // Act
        var wrapped = TelemetryAIFunctionWrapper.WrapAllWithCreditTracking(
            functions,
            _telemetryClientMock.Object,
            _loggerMock.Object,
            _creditTrackingServiceMock.Object,
            _pluginRepositoryMock.Object,
            "test_tool");

        // Assert
        wrapped.Should().HaveCount(3);
        wrapped.Should().AllSatisfy(f => f.Should().BeOfType<TelemetryAIFunctionWrapper>());
        wrapped.Select(f => f.Name).Should().BeEquivalentTo(new[] { "Func1", "Func2", "Func3" });
    }
}
