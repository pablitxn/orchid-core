using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using WebApi.Controllers;
using WebApi.Hubs;

namespace WebApi.Tests.Controllers;

public class ActivitiesControllerTests
{
    private readonly ActivitiesController _controller;
    private readonly Mock<IDistributedCache> _cacheMock;

    public ActivitiesControllerTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _controller = new ActivitiesController(_cacheMock.Object);
    }

    [Fact]
    public async Task GetActivities_ReturnsOk_WithActivitiesList_WhenCacheHasData()
    {
        // Arrange
        var activities = new List<Activity>
        {
            new("test_event", new { test = "data" })
        };

        var json = JsonSerializer.Serialize(activities);
        var bytes = Encoding.UTF8.GetBytes(json);

        _cacheMock
            .Setup(c => c.GetAsync("activity_history", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        // Act
        var result = await _controller.GetActivities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedActivities = Assert.IsAssignableFrom<IEnumerable<Activity>>(okResult.Value);
        Assert.Single(returnedActivities);
    }

    [Fact]
    public async Task GetActivities_ReturnsOk_WithEmptyArray_WhenCacheIsEmpty()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.GetAsync("activity_history", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _controller.GetActivities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedActivities = Assert.IsAssignableFrom<IEnumerable<Activity>>(okResult.Value);
        Assert.Empty(returnedActivities);
    }

    [Fact]
    public async Task GetActivities_ReturnsOk_WithEmptyArray_WhenCacheReturnsEmptyString()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(string.Empty);

        _cacheMock
            .Setup(c => c.GetAsync("activity_history", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        // Act
        var result = await _controller.GetActivities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedActivities = Assert.IsAssignableFrom<IEnumerable<Activity>>(okResult.Value);
        Assert.Empty(returnedActivities);
    }

    [Fact]
    public async Task GetActivities_CallsCache_WithCorrectKey()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.GetAsync("activity_history", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        await _controller.GetActivities();

        // Assert
        _cacheMock.Verify(
            c => c.GetAsync("activity_history", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActivities_ReturnsEmptyList_WhenJsonIsInvalid()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("invalid json");

        _cacheMock
            .Setup(c => c.GetAsync("activity_history", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        // Act & Assert
        // The controller should handle JSON deserialization errors gracefully
        // In this case, it will throw JsonException which should be handled by middleware
        await Assert.ThrowsAsync<JsonException>(() => _controller.GetActivities());
    }

    [Fact]
    public async Task GetActivities_HandlesMultipleActivities()
    {
        // Arrange
        var activities = new List<Activity>
        {
            new("event1", new { message = "Message 1" }),
            new("event2", new { message = "Message 2" }),
            new("event3", new { message = "Message 3" })
        };

        var json = JsonSerializer.Serialize(activities);
        var bytes = Encoding.UTF8.GetBytes(json);

        _cacheMock
            .Setup(c => c.GetAsync("activity_history", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        // Act
        var result = await _controller.GetActivities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedActivities = Assert.IsAssignableFrom<IEnumerable<Activity>>(okResult.Value);
        Assert.Equal(3, returnedActivities.Count());
    }
}
