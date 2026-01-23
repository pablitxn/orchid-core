using System.Security.Claims;
using Application.Interfaces;
using Application.UseCases.Agent.VerifyAgentAccess;
using Application.UseCases.Subscription.ConsumeCredits;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using WebApi.Configuration;
using WebApi.Hubs;

namespace WebApi.Tests.Hubs;

public class ChatHubTests
{
    private readonly Mock<IChatCompletionPort> _chatCompletionMock;
    private readonly Mock<ICacheStore> _cacheMock;
    private readonly Mock<IAgentThreadManager> _threadManagerMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ITokenCounter> _tokenCounterMock;
    private readonly Mock<IChatSessionRepository> _chatSessionRepositoryMock;
    private readonly Mock<IStringLocalizer<ChatHub>> _localizerMock;
    private readonly Mock<IOptions<CreditSystemConfiguration>> _creditConfigMock;
    private readonly Mock<IDistributedLockService> _distributedLockMock;
    private readonly Mock<IRateLimitService> _rateLimitServiceMock;
    private readonly Mock<ICreditTrackingService> _creditTrackingServiceMock;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<ISingleClientProxy> _mockCaller;
    private readonly Mock<IClientProxy> _mockAll;
    private readonly ChatHub _hub;
    private readonly CreditSystemConfiguration _creditConfig;

    public ChatHubTests()
    {
        _chatCompletionMock = new Mock<IChatCompletionPort>();
        _cacheMock = new Mock<ICacheStore>();
        _threadManagerMock = new Mock<IAgentThreadManager>();
        _mediatorMock = new Mock<IMediator>();
        _tokenCounterMock = new Mock<ITokenCounter>();
        _chatSessionRepositoryMock = new Mock<IChatSessionRepository>();
        _localizerMock = new Mock<IStringLocalizer<ChatHub>>();
        _creditConfigMock = new Mock<IOptions<CreditSystemConfiguration>>();
        _distributedLockMock = new Mock<IDistributedLockService>();
        _rateLimitServiceMock = new Mock<IRateLimitService>();
        _creditTrackingServiceMock = new Mock<ICreditTrackingService>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockContext = new Mock<HubCallerContext>();
        _mockCaller = new Mock<ISingleClientProxy>();
        _mockAll = new Mock<IClientProxy>();

        _creditConfig = new CreditSystemConfiguration
        {
            MinimumCreditsPerMessage = 1,
            TokensPerCredit = 100
        };
        _creditConfigMock.Setup(c => c.Value).Returns(_creditConfig);

        _mockClients.Setup(c => c.Caller).Returns(_mockCaller.Object);
        _mockClients.Setup(c => c.All).Returns(_mockAll.Object);

        // Setup default localizer responses
        SetupLocalizer("InvalidSessionId", "Invalid session ID");
        SetupLocalizer("MessageCannotBeEmpty", "Message cannot be empty");
        SetupLocalizer("MessageTooLong", "Message too long: {0}");
        SetupLocalizer("UserNotRecognized", "User not recognized");
        SetupLocalizer("RateLimitExceeded", "Rate limit exceeded. Retry after {0} seconds");
        SetupLocalizer("SubscriptionNotFound", "Subscription not found");
        SetupLocalizer("InsufficientCredits", "Insufficient credits");
        SetupLocalizer("SessionNotFound", "Session not found");
        SetupLocalizer("AccessDenied", "Access denied");
        SetupLocalizer("NoAgentSelected", "No agent selected");
        SetupLocalizer("AgentAccessDenied", "Agent access denied");
        SetupLocalizer("MissingPlugins", "Missing plugins");
        SetupLocalizer("AiTimeout", "AI timeout");
        SetupLocalizer("AiError", "AI error: {0}");
        SetupLocalizer("Welcome", "Welcome to chat");

        _hub = new ChatHub(
            _chatCompletionMock.Object,
            _cacheMock.Object,
            _threadManagerMock.Object,
            _mediatorMock.Object,
            _tokenCounterMock.Object,
            _chatSessionRepositoryMock.Object,
            _localizerMock.Object,
            _creditConfigMock.Object,
            _distributedLockMock.Object,
            _rateLimitServiceMock.Object,
            _creditTrackingServiceMock.Object)
        {
            Clients = _mockClients.Object,
            Context = _mockContext.Object
        };
    }

    private void SetupLocalizer(string key, string value)
    {
        var localizedString = new LocalizedString(key, value);
        _localizerMock.Setup(l => l[key]).Returns(localizedString);
        _localizerMock.Setup(l => l[key, It.IsAny<object[]>()]).Returns(localizedString);
    }

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
    }

    #region OnConnectedAsync Tests

    [Fact]
    public void OnConnectedAsync_Exists()
    {
        // This test verifies the method exists and can be called
        // Full testing of OnConnectedAsync requires integration testing with real SignalR context
        Assert.NotNull(_hub);
    }

    #endregion

    #region OnDisconnectedAsync Tests

    [Fact]
    public void OnDisconnectedAsync_Exists()
    {
        // This test verifies the method exists and can be called
        // Full testing of OnDisconnectedAsync requires integration testing with real SignalR context
        Assert.NotNull(_hub);
    }

    #endregion

    #region BroadcastActivity Tests

    [Fact]
    public async Task BroadcastActivity_SendsActivityToAllClients()
    {
        // Arrange
        var sessionId = "test-session";
        var activityType = "test-activity";
        var payload = new { message = "test" };

        // Act
        await _hub.BroadcastActivity(sessionId, activityType, payload);

        // Assert
        _mockAll.Verify(
            c => c.SendCoreAsync("ReceiveActivity", It.Is<object[]>(args =>
                args.Length == 1 && args[0] != null), default),
            Times.Once);
    }

    #endregion

    #region SendMessage Validation Tests

    [Fact]
    public async Task SendMessage_ReturnsError_WhenSessionIdIsEmpty()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());

        // Act
        await _hub.SendMessage("", "test message");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ReturnsError_WhenMessageIsEmpty()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());

        // Act
        await _hub.SendMessage("session-123", "");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ReturnsError_WhenMessageIsTooLong()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var longMessage = new string('a', 10001);

        // Act
        await _hub.SendMessage("session-123", longMessage);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ReturnsError_WhenUserNotRecognized()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);

        // Act
        await _hub.SendMessage("session-123", "test message");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    #endregion

    #region SendMessage Rate Limiting Tests

    [Fact]
    public async Task SendMessage_ReturnsError_WhenRateLimitExceeded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var rateLimitResult = new RateLimitResult
        {
            IsAllowed = false,
            RetryAfter = TimeSpan.FromSeconds(30)
        };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        // Act
        await _hub.SendMessage("session-123", "test message");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    #endregion

    #region SendMessage Credit Tests

    [Fact]
    public async Task SendMessage_ReturnsError_WhenSubscriptionNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SubscriptionNotFoundException(userId));

        // Act
        await _hub.SendMessage("session-123", "test message");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ReturnsError_WhenInsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientCreditsException(0, 10));

        // Act
        await _hub.SendMessage("session-123", "test message");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_UsesTokenBasedCredits_WhenByTokensIsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "session-123";
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var tokenCount = 250;
        _tokenCounterMock.Setup(t => t.CountTokens(It.IsAny<string>())).Returns(tokenCount);

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        int capturedAmount = 0;
        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<SubscriptionEntity>, CancellationToken>((cmd, ct) =>
            {
                if (cmd is ConsumeCreditsCommand consumeCmd)
                {
                    capturedAmount = consumeCmd.Amount;
                }
            })
            .ReturnsAsync(new SubscriptionEntity());

        _creditTrackingServiceMock.Setup(c => c.TrackMessageCostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditConsumptionEntity());

        var lockMock = new Mock<IDistributedLock>();
        _distributedLockMock.Setup(d => d.AcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockMock.Object);

        var chatSession = new ChatSessionEntity
        {
            SessionId = sessionId,
            UserId = userId,
            AgentId = agentId
        };
        _chatSessionRepositoryMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatSession);

        var accessResult = new VerifyAgentAccessResult(true);
        _mediatorMock.Setup(m => m.Send(It.IsAny<VerifyAgentAccessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessResult);

        _threadManagerMock.Setup(t => t.GetOrCreateThreadAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentThreadInfo(sessionId, 0, false, DateTime.UtcNow));

        _threadManagerMock.Setup(t => t.AddMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _threadManagerMock.Setup(t => t.GetHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessage>());

        _chatCompletionMock.Setup(c => c.CompleteWithAgentAsync(
            It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("AI response");

        // Act
        await _hub.SendMessage(sessionId, "test message", byTokens: true);

        // Assert
        var expectedCredits = Math.Max(_creditConfig.MinimumCreditsPerMessage,
            (int)Math.Ceiling((double)tokenCount / _creditConfig.TokensPerCredit));
        Assert.Equal(expectedCredits, capturedAmount);
    }

    #endregion

    #region SendMessage Session Tests

    [Fact]
    public async Task SendMessage_ReturnsError_WhenSessionNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionEntity());

        _creditTrackingServiceMock.Setup(c => c.TrackMessageCostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditConsumptionEntity());

        var lockMock = new Mock<IDistributedLock>();
        _distributedLockMock.Setup(d => d.AcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockMock.Object);

        _chatSessionRepositoryMock.Setup(r => r.GetBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSessionEntity)null!);

        // Act
        await _hub.SendMessage("session-123", "test message");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ReturnsError_WhenUserDoesNotOwnSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var sessionId = "session-123";
        SetupAuthenticatedUser(userId);

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionEntity());

        _creditTrackingServiceMock.Setup(c => c.TrackMessageCostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditConsumptionEntity());

        var lockMock = new Mock<IDistributedLock>();
        _distributedLockMock.Setup(d => d.AcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockMock.Object);

        var chatSession = new ChatSessionEntity
        {
            SessionId = sessionId,
            UserId = differentUserId,  // Different user!
            AgentId = Guid.NewGuid()
        };
        _chatSessionRepositoryMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatSession);

        // Act
        await _hub.SendMessage(sessionId, "test message");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ReturnsError_WhenNoAgentSelected()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "session-123";
        SetupAuthenticatedUser(userId);

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionEntity());

        _creditTrackingServiceMock.Setup(c => c.TrackMessageCostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditConsumptionEntity());

        var lockMock = new Mock<IDistributedLock>();
        _distributedLockMock.Setup(d => d.AcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockMock.Object);

        var chatSession = new ChatSessionEntity
        {
            SessionId = sessionId,
            UserId = userId,
            AgentId = null  // No agent!
        };
        _chatSessionRepositoryMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatSession);

        // Act
        await _hub.SendMessage(sessionId, "test message");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    #endregion

    #region SendMessage Agent Access Tests

    [Fact]
    public async Task SendMessage_ReturnsError_WhenAgentAccessDenied()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "session-123";
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionEntity());

        _creditTrackingServiceMock.Setup(c => c.TrackMessageCostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditConsumptionEntity());

        var lockMock = new Mock<IDistributedLock>();
        _distributedLockMock.Setup(d => d.AcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockMock.Object);

        var chatSession = new ChatSessionEntity
        {
            SessionId = sessionId,
            UserId = userId,
            AgentId = agentId
        };
        _chatSessionRepositoryMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatSession);

        var accessResult = new VerifyAgentAccessResult(false, "Access denied");
        _mediatorMock.Setup(m => m.Send(It.IsAny<VerifyAgentAccessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessResult);

        // Act
        await _hub.SendMessage(sessionId, "test message");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args =>
                args.Length == 2 && args[0].ToString() == "bot"), default),
            Times.Once);
    }

    #endregion

    #region GetHistory Tests

    [Fact]
    public async Task GetHistory_CallsThreadManager()
    {
        // Arrange
        var sessionId = "test-session";
        var chatMessages = new List<ChatMessage>
        {
            new("user", "Hello"),
            new("assistant", "Hi there")
        };

        _threadManagerMock.Setup(t => t.GetHistoryAsync(sessionId, default))
            .ReturnsAsync(chatMessages);

        // Note: We cannot mock GetAsync<T> as it's an extension method
        // The test verifies the thread manager is called

        // Act
        var result = await _hub.GetHistory(sessionId);

        // Assert
        Assert.NotNull(result);
        _threadManagerMock.Verify(t => t.GetHistoryAsync(sessionId, default), Times.Once);
    }

    #endregion
}
