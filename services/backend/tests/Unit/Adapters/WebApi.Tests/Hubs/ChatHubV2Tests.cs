using System.Security.Claims;
using Application.Interfaces;
using Application.Models;
using Application.UseCases.Agent.VerifyAgentAccess;
using Application.UseCases.ChatV2.Common;
using Application.UseCases.ChatV2.ListMessages;
using Application.UseCases.ChatV2.SendMessage;
using Application.UseCases.ChatV2.ToggleReaction;
using Application.UseCases.Subscription.ConsumeCredits;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using WebApi.Configuration;
using WebApi.Hubs;
using WebApi.Models.V2;

namespace WebApi.Tests.Hubs;

public class ChatHubV2Tests
{
    private readonly Mock<IChatCompletionPort> _chatCompletionMock;
    private readonly Mock<ICacheStore> _cacheMock;
    private readonly Mock<IAgentThreadManager> _threadManagerMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ITokenCounter> _tokenCounterMock;
    private readonly Mock<IChatSessionRepository> _chatSessionRepositoryMock;
    private readonly Mock<IStringLocalizer<ChatHubV2>> _localizerMock;
    private readonly Mock<IOptions<CreditSystemConfiguration>> _creditConfigMock;
    private readonly Mock<IDistributedLockService> _distributedLockMock;
    private readonly Mock<IRateLimitService> _rateLimitServiceMock;
    private readonly Mock<ICreditTrackingService> _creditTrackingServiceMock;
    private readonly Mock<IModelResolver> _modelResolverMock;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<IUserCreditsRepository> _userCreditsRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<ISingleClientProxy> _mockCaller;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly ChatHubV2 _hub;
    private readonly CreditSystemConfiguration _creditConfig;

    public ChatHubV2Tests()
    {
        _chatCompletionMock = new Mock<IChatCompletionPort>();
        _cacheMock = new Mock<ICacheStore>();
        _threadManagerMock = new Mock<IAgentThreadManager>();
        _mediatorMock = new Mock<IMediator>();
        _tokenCounterMock = new Mock<ITokenCounter>();
        _chatSessionRepositoryMock = new Mock<IChatSessionRepository>();
        _localizerMock = new Mock<IStringLocalizer<ChatHubV2>>();
        _creditConfigMock = new Mock<IOptions<CreditSystemConfiguration>>();
        _distributedLockMock = new Mock<IDistributedLockService>();
        _rateLimitServiceMock = new Mock<IRateLimitService>();
        _creditTrackingServiceMock = new Mock<ICreditTrackingService>();
        _modelResolverMock = new Mock<IModelResolver>();
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _userCreditsRepositoryMock = new Mock<IUserCreditsRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockContext = new Mock<HubCallerContext>();
        _mockCaller = new Mock<ISingleClientProxy>();
        _mockGroups = new Mock<IGroupManager>();

        _creditConfig = new CreditSystemConfiguration
        {
            MinimumCreditsPerMessage = 1,
            TokensPerCredit = 100
        };
        _creditConfigMock.Setup(c => c.Value).Returns(_creditConfig);

        _mockClients.Setup(c => c.Caller).Returns(_mockCaller.Object);

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
        SetupLocalizer("AiError", "AI error: {0}");

        // Default model resolver setup
        _modelResolverMock
            .Setup(r => r.ResolveAffordableModelAsync(
                It.IsAny<ModelResolutionContext>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelResolutionResult
            {
                ModelId = "gpt-5-nano-2025-08-07",
                Source = ModelResolutionSource.SystemDefault,
                EstimatedCreditCost = 1
            });

        // Default subscription with credits
        _subscriptionRepositoryMock
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionEntity { Credits = 100 });

        // Default user with auto-degradation enabled
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEntity { AllowAutoDegradation = true });

        _hub = new ChatHubV2(
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
            _creditTrackingServiceMock.Object,
            _modelResolverMock.Object,
            _subscriptionRepositoryMock.Object,
            _userCreditsRepositoryMock.Object,
            _userRepositoryMock.Object)
        {
            Clients = _mockClients.Object,
            Context = _mockContext.Object,
            Groups = _mockGroups.Object
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
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        _mockContext.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);
    }

    private void SetupUnauthenticatedUser()
    {
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
    }

    #region SendMessageV2 Validation Tests

    [Fact]
    public async Task SendMessageV2_SendsError_WhenSessionIdIsEmpty()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var request = new SendMessageV2Request("test message");

        // Act
        await _hub.SendMessageV2("", request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageV2_SendsError_WhenMessageIsEmpty()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var request = new SendMessageV2Request("");

        // Act
        await _hub.SendMessageV2("session-123", request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageV2_SendsError_WhenMessageIsTooLong()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var longMessage = new string('a', 10001);
        var request = new SendMessageV2Request(longMessage);

        // Act
        await _hub.SendMessageV2("session-123", request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageV2_SendsError_WhenUserNotRecognized()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var request = new SendMessageV2Request("test message");

        // Act
        await _hub.SendMessageV2("session-123", request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    #endregion

    #region SendMessageV2 Rate Limiting Tests

    [Fact]
    public async Task SendMessageV2_SendsError_WhenRateLimitExceeded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new SendMessageV2Request("test message");

        var rateLimitResult = new RateLimitResult
        {
            IsAllowed = false,
            RetryAfter = TimeSpan.FromSeconds(30)
        };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        // Act
        await _hub.SendMessageV2("session-123", request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    #endregion

    #region SendMessageV2 Credit Tests

    [Fact]
    public async Task SendMessageV2_SendsError_WhenSubscriptionNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new SendMessageV2Request("test message");

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SubscriptionNotFoundException(userId));

        // Act
        await _hub.SendMessageV2("session-123", request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageV2_SendsError_WhenInsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new SendMessageV2Request("test message");

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientCreditsException(0, 10));

        // Act
        await _hub.SendMessageV2("session-123", request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    #endregion

    #region SendMessageV2 Session Tests

    [Fact]
    public async Task SendMessageV2_SendsError_WhenSessionNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new SendMessageV2Request("test message");

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.UseCases.Subscription.Common.SubscriptionDto());

        _creditTrackingServiceMock.Setup(c => c.TrackMessageCostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditConsumptionEntity());

        var lockMock = new Mock<IDistributedLock>();
        _distributedLockMock.Setup(d => d.WaitAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockMock.Object);

        _chatSessionRepositoryMock.Setup(r => r.GetBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatSessionEntity)null!);

        // Act
        await _hub.SendMessageV2("session-123", request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageV2_SendsError_WhenUserDoesNotOwnSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var sessionId = "session-123";
        SetupAuthenticatedUser(userId);
        var request = new SendMessageV2Request("test message");

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.UseCases.Subscription.Common.SubscriptionDto());

        _creditTrackingServiceMock.Setup(c => c.TrackMessageCostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditConsumptionEntity());

        var lockMock = new Mock<IDistributedLock>();
        _distributedLockMock.Setup(d => d.WaitAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
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
        await _hub.SendMessageV2(sessionId, request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageV2_SendsError_WhenNoAgentSelected()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "session-123";
        SetupAuthenticatedUser(userId);
        var request = new SendMessageV2Request("test message");

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.UseCases.Subscription.Common.SubscriptionDto());

        _creditTrackingServiceMock.Setup(c => c.TrackMessageCostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditConsumptionEntity());

        var lockMock = new Mock<IDistributedLock>();
        _distributedLockMock.Setup(d => d.WaitAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
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
        await _hub.SendMessageV2(sessionId, request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    #endregion

    #region SendMessageV2 Agent Access Tests

    [Fact]
    public async Task SendMessageV2_SendsError_WhenAgentAccessDenied()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "session-123";
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new SendMessageV2Request("test message");

        var rateLimitResult = new RateLimitResult { IsAllowed = true };
        _rateLimitServiceMock.Setup(r => r.CheckAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimitResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.UseCases.Subscription.Common.SubscriptionDto());

        _creditTrackingServiceMock.Setup(c => c.TrackMessageCostAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditConsumptionEntity());

        var lockMock = new Mock<IDistributedLock>();
        _distributedLockMock.Setup(d => d.WaitAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockMock.Object);

        var chatSession = new ChatSessionEntity
        {
            SessionId = sessionId,
            UserId = userId,
            AgentId = agentId
        };
        _chatSessionRepositoryMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatSession);

        var accessResult = new VerifyAgentAccessDto(false, "Access denied");
        _mediatorMock.Setup(m => m.Send(It.IsAny<VerifyAgentAccessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessResult);

        // Act
        await _hub.SendMessageV2(sessionId, request);

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    #endregion

    #region GetHistoryV2 Tests

    [Fact]
    public async Task GetHistoryV2_ReturnsMessages_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "test-session";
        SetupAuthenticatedUser(userId);

        var expectedMessages = new PaginatedMessagesDto
        {
            Messages = new List<MessageV2Dto>
            {
                new MessageV2Dto { Id = Guid.NewGuid().ToString(), Role = "user", Content = "Hello", Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                new MessageV2Dto { Id = Guid.NewGuid().ToString(), Role = "assistant", Content = "Hi there!", Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
            },
            HasMore = false,
            TotalCount = 2
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListMessagesV2Query>(q => q.SessionId == sessionId && q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMessages);

        // Act
        var result = await _hub.GetHistoryV2(sessionId);

        // Assert
        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetHistoryV2_ThrowsUnauthorizedDomainAccessException_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var sessionId = "test-session";

        // Act
        var act = async () => await _hub.GetHistoryV2(sessionId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedDomainAccessException>();
    }

    [Fact]
    public async Task GetHistoryV2_SendsCorrectQueryParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "test-session";
        var cursor = "test-cursor";
        var limit = 25;
        var direction = "forward";
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListMessagesV2Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedMessagesDto
            {
                Messages = new List<MessageV2Dto>(),
                HasMore = false,
                TotalCount = 0
            });

        // Act
        await _hub.GetHistoryV2(sessionId, cursor, limit, direction);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ListMessagesV2Query>(q =>
                q.SessionId == sessionId &&
                q.UserId == userId &&
                q.Cursor == cursor &&
                q.Limit == limit &&
                q.Direction == direction),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ToggleReaction Tests

    [Fact]
    public async Task ToggleReaction_SendsError_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var sessionId = "test-session";
        var messageId = Guid.NewGuid().ToString();

        // Act
        await _hub.ToggleReaction(sessionId, messageId, "thumbs_up", "like");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task ToggleReaction_SendsError_WhenMessageIdInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var sessionId = "test-session";
        var invalidMessageId = "not-a-guid";

        // Act
        await _hub.ToggleReaction(sessionId, invalidMessageId, "thumbs_up", "like");

        // Assert
        _mockCaller.Verify(
            c => c.SendCoreAsync("Error", It.IsAny<object[]>(), default),
            Times.Once);
    }

    #endregion

    #region Connection Lifecycle Tests

    [Fact]
    public void ChatHubV2_CanBeInstantiated()
    {
        // This test verifies the hub can be instantiated with all dependencies
        Assert.NotNull(_hub);
    }

    #endregion
}
