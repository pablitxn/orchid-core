using System.Text.Json;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Ai;

public class SafeAIFunctionWrapperTests
{
    private readonly Mock<ILogger<SafeAIFunctionWrapperTests>> _loggerMock;

    public SafeAIFunctionWrapperTests()
    {
        _loggerMock = new Mock<ILogger<SafeAIFunctionWrapperTests>>();
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnResult_When_FunctionSucceeds()
    {
        // Arrange
        var expectedResult = "success result";
        var innerFunction = AIFunctionFactory.Create(() => expectedResult, "TestFunction");
        var wrapper = new SafeAIFunctionWrapper(innerFunction, _loggerMock.Object);

        // Act
        var result = await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert - result may be wrapped, compare string content
        result?.ToString().Should().Be(expectedResult);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnErrorJson_When_FunctionThrows()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(
            () => ThrowInvalidOperation("Test error"),
            "FailingFunction");
        var wrapper = new SafeAIFunctionWrapper(innerFunction, _loggerMock.Object);

        // Act
        var result = await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var resultString = result?.ToString();
        resultString.Should().NotBeNull();

        var errorResponse = JsonSerializer.Deserialize<JsonElement>(resultString!);
        errorResponse.GetProperty("success").GetBoolean().Should().BeFalse();
        errorResponse.GetProperty("error").GetString().Should().Be("InvalidOperationException");
        errorResponse.GetProperty("message").GetString().Should().Contain("Test error");
        errorResponse.GetProperty("recoverable").GetBoolean().Should().BeFalse();
    }

    private static string ThrowInvalidOperation(string message)
    {
        throw new InvalidOperationException(message);
    }

    private static string ThrowHttpRequest(string message)
    {
        throw new HttpRequestException(message);
    }

    private static string ThrowTimeout(string message)
    {
        throw new TimeoutException(message);
    }

    private static string ThrowOperationCanceled(string message)
    {
        throw new OperationCanceledException(message);
    }

    [Fact]
    public async Task InvokeAsync_Should_MarkAsRecoverable_When_HttpRequestException()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(
            () => ThrowHttpRequest("Network error"),
            "NetworkFunction");
        var wrapper = new SafeAIFunctionWrapper(innerFunction, _loggerMock.Object);

        // Act
        var result = await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var resultString = result?.ToString();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(resultString!);
        errorResponse.GetProperty("recoverable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_MarkAsRecoverable_When_TimeoutException()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(
            () => ThrowTimeout("Operation timed out"),
            "TimeoutFunction");
        var wrapper = new SafeAIFunctionWrapper(innerFunction, _loggerMock.Object);

        // Act
        var result = await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var resultString = result?.ToString();
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(resultString!);
        errorResponse.GetProperty("recoverable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_PropagateOperationCanceledException()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(
            () => ThrowOperationCanceled("Cancelled"),
            "CancelledFunction");
        var wrapper = new SafeAIFunctionWrapper(innerFunction, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task InvokeAsync_Should_LogError_When_FunctionThrows()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(
            () => ThrowInvalidOperation("Test error"),
            "LoggingTestFunction");
        var wrapper = new SafeAIFunctionWrapper(innerFunction, _loggerMock.Object);

        // Act
        await wrapper.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("LoggingTestFunction")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Name_Should_ReturnInnerFunctionName()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(() => "result", "MyFunctionName");
        var wrapper = new SafeAIFunctionWrapper(innerFunction, _loggerMock.Object);

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
        var wrapper = new SafeAIFunctionWrapper(innerFunction, _loggerMock.Object);

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
        var wrapped = SafeAIFunctionWrapper.WrapAll(functions, _loggerMock.Object);

        // Assert
        wrapped.Should().HaveCount(3);
        wrapped.Should().AllSatisfy(f => f.Should().BeOfType<SafeAIFunctionWrapper>());
        wrapped.Select(f => f.Name).Should().BeEquivalentTo(new[] { "Func1", "Func2", "Func3" });
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_InnerFunctionIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SafeAIFunctionWrapper(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Arrange
        var innerFunction = AIFunctionFactory.Create(() => "result", "TestFunc");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SafeAIFunctionWrapper(innerFunction, null!));
    }

    [Fact]
    public async Task InvokeAsync_Should_PassArgumentsToInnerFunction()
    {
        // Arrange
        string? receivedArg = null;
        var innerFunction = AIFunctionFactory.Create(
            (string input) =>
            {
                receivedArg = input;
                return $"processed: {input}";
            },
            "ArgFunction");
        var wrapper = new SafeAIFunctionWrapper(innerFunction, _loggerMock.Object);

        var arguments = new AIFunctionArguments
        {
            ["input"] = "test value"
        };

        // Act
        var result = await wrapper.InvokeAsync(arguments, CancellationToken.None);

        // Assert
        receivedArg.Should().Be("test value");
        result?.ToString().Should().Be("processed: test value");
    }
}
