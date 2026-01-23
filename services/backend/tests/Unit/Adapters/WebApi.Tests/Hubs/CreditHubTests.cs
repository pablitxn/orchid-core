using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Hubs;

namespace WebApi.Tests.Hubs;

public class CreditHubTests
{
    private readonly Mock<ILogger<CreditHub>> _loggerMock;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly CreditHub _hub;

    public CreditHubTests()
    {
        _loggerMock = new Mock<ILogger<CreditHub>>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _hub = new CreditHub(_loggerMock.Object)
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
    public async Task SubscribeToCreditUpdates_SendsSubscribedMessage_WhenUserIdExists()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var mockCaller = new Mock<ISingleClientProxy>();

        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
        _mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);

        // Act
        await _hub.SubscribeToCreditUpdates();

        // Assert
        mockCaller.Verify(
            c => c.SendCoreAsync(
                "Subscribed",
                It.Is<object[]>(args =>
                    args.Length == 1 &&
                    args[0] != null),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeToCreditUpdates_DoesNotSendMessage_WhenUserIdIsNull()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var mockCaller = new Mock<ISingleClientProxy>();

        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
        _mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);

        // Act
        await _hub.SubscribeToCreditUpdates();

        // Assert
        mockCaller.Verify(
            c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default),
            Times.Never);
    }
}
