using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Middleware;

namespace WebApi.Tests.Middleware;

public class GlobalExceptionMiddlewareTests
{
    private readonly Mock<ILogger<GlobalExceptionMiddleware>> _loggerMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;

    public GlobalExceptionMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
    }

    private GlobalExceptionMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new GlobalExceptionMiddleware(next, _loggerMock.Object, _environmentMock.Object);
    }

    /// <summary>
    /// Extracts and deserializes ProblemDetails from the HTTP response body.
    /// </summary>
    /// <param name="context">The HTTP context containing the response with ProblemDetails JSON.</param>
    /// <returns>The deserialized ProblemDetails object, or null if deserialization fails.</returns>
    private static async Task<ProblemDetails?> GetProblemDetailsFromResponse(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ProblemDetails>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    [Fact]
    public async Task InvokeAsync_Returns402_WhenInsufficientCreditsException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Insufficient credits"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.PaymentRequired, context.Response.StatusCode);
        var problemDetails = await GetProblemDetailsFromResponse(context);
        Assert.NotNull(problemDetails);
        Assert.Equal("Insufficient Credits", problemDetails.Title);
        Assert.Equal("You don't have enough credits to complete this action.", problemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_Returns409_WhenAlreadyPurchasedException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Item already purchased"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        var problemDetails = await GetProblemDetailsFromResponse(context);
        Assert.NotNull(problemDetails);
        Assert.Equal("Already Purchased", problemDetails.Title);
        Assert.Equal("You have already purchased this item.", problemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_Returns404_WhenKeyNotFoundException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("Resource not found"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
        var problemDetails = await GetProblemDetailsFromResponse(context);
        Assert.NotNull(problemDetails);
        Assert.Equal("Resource Not Found", problemDetails.Title);
        Assert.Equal("The requested resource was not found.", problemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_Returns403_WhenUnauthorizedAccessException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var middleware = CreateMiddleware(_ => throw new UnauthorizedAccessException("Access denied"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.Forbidden, context.Response.StatusCode);
        var problemDetails = await GetProblemDetailsFromResponse(context);
        Assert.NotNull(problemDetails);
        Assert.Equal("Forbidden", problemDetails.Title);
        Assert.Equal("You are not authorized to perform this action.", problemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_Returns400_WhenArgumentException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        const string errorMessage = "Invalid argument provided";
        var middleware = CreateMiddleware(_ => throw new ArgumentException(errorMessage));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        var problemDetails = await GetProblemDetailsFromResponse(context);
        Assert.NotNull(problemDetails);
        Assert.Equal("Invalid Request", problemDetails.Title);
        Assert.Equal(errorMessage, problemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_Returns500_WhenUnknownException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var middleware = CreateMiddleware(_ => throw new Exception("Something went wrong"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        var problemDetails = await GetProblemDetailsFromResponse(context);
        Assert.NotNull(problemDetails);
        Assert.Equal("Internal Server Error", problemDetails.Title);
        Assert.Equal("An error occurred while processing your request.", problemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_IncludesCorrelationIdInProblemDetails()
    {
        // Arrange
        const string expectedCorrelationId = "my-correlation-id-123";
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = expectedCorrelationId;

        var middleware = CreateMiddleware(_ => throw new Exception("Test exception"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        
        Assert.Contains(expectedCorrelationId, json);
    }

    [Fact]
    public async Task InvokeAsync_UsesTraceIdentifier_WhenCorrelationIdNotInItems()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "trace-id-fallback";

        var middleware = CreateMiddleware(_ => throw new Exception("Test exception"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        
        Assert.Contains("trace-id-fallback", json);
    }

    [Fact]
    public async Task InvokeAsync_IncludesStackTraceInDevelopment()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var middleware = CreateMiddleware(_ => throw new Exception("Test exception"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        
        Assert.Contains("stackTrace", json);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotIncludeStackTraceInProduction()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var middleware = CreateMiddleware(_ => throw new Exception("Test exception"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        
        Assert.DoesNotContain("stackTrace", json);
    }

    [Fact]
    public async Task InvokeAsync_ShowsExceptionMessageInDevelopment()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        const string errorMessage = "Detailed error message for debugging";
        var middleware = CreateMiddleware(_ => throw new Exception(errorMessage));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var problemDetails = await GetProblemDetailsFromResponse(context);
        Assert.NotNull(problemDetails);
        Assert.Equal(errorMessage, problemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_HidesExceptionMessageInProduction()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        const string sensitiveMessage = "Database connection string: secret";
        var middleware = CreateMiddleware(_ => throw new Exception(sensitiveMessage));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var problemDetails = await GetProblemDetailsFromResponse(context);
        Assert.NotNull(problemDetails);
        Assert.Equal("An error occurred while processing your request.", problemDetails.Detail);
        Assert.DoesNotContain(sensitiveMessage, problemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_SetsContentTypeToJson()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var middleware = CreateMiddleware(_ => throw new Exception("Test exception"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_LogsError_WhenExceptionOccurs()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var exception = new Exception("Test exception");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware_WhenNoException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;

        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_IncludesErrorCodeInProblemDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-corr-id";

        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException());

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        
        Assert.Contains("NOT_FOUND", json);
    }
}
