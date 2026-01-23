using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Hubs;

namespace WebApi.Tests.Hubs;

public class NotificationHubTests
{
    private readonly Mock<ILogger<NotificationHub>> _loggerMock;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly NotificationHub _hub;

    public NotificationHubTests()
    {
        _loggerMock = new Mock<ILogger<NotificationHub>>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _hub = new NotificationHub(_loggerMock.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task OnConnectedAsync_AddsUserToPersonalGroup_WhenUserIdExists()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var connectionId = "test-connection-id";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
        _mockGroups.Setup(g => g.AddToGroupAsync(connectionId, $"user-{userId}", default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockGroups.Verify(g => g.AddToGroupAsync(connectionId, $"user-{userId}", default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_DoesNotAddToGroup_WhenUserIdIsNull()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockGroups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_DoesNotAddToGroup_WhenUserIdIsEmpty()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, string.Empty)
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockGroups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_RemovesUserFromPersonalGroup_WhenUserIdExists()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var connectionId = "test-connection-id";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
        _mockGroups.Setup(g => g.RemoveFromGroupAsync(connectionId, $"user-{userId}", default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _mockGroups.Verify(g => g.RemoveFromGroupAsync(connectionId, $"user-{userId}", default), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNotRemoveFromGroup_WhenUserIdIsNull()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _mockGroups.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_HandlesException_WhenExceptionProvided()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var connectionId = "test-connection-id";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var exception = new Exception("Test exception");

        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
        _mockGroups.Setup(g => g.RemoveFromGroupAsync(connectionId, $"user-{userId}", default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.OnDisconnectedAsync(exception);

        // Assert
        _mockGroups.Verify(g => g.RemoveFromGroupAsync(connectionId, $"user-{userId}", default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_LogsUserConnection()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var connectionId = "test-connection-id";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
        _mockGroups.Setup(g => g.AddToGroupAsync(connectionId, $"user-{userId}", default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.OnConnectedAsync();

        // Assert - verify logging occurred (LogInformation is called)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(userId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_LogsUserDisconnection()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var connectionId = "test-connection-id";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
        _mockGroups.Setup(g => g.RemoveFromGroupAsync(connectionId, $"user-{userId}", default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert - verify logging occurred
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(userId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
