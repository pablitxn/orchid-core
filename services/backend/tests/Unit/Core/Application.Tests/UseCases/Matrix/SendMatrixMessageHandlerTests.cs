using Application.Interfaces;
using Application.UseCases.Agent.VerifyAgentAccess;
using Application.UseCases.Matrix.SendMessage;
using Application.UseCases.Subscription.Common;
using Application.UseCases.Subscription.ConsumeCredits;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.Matrix;

public class SendMatrixMessageHandlerTests
{
    private readonly SendMatrixMessageHandler _handler;
    private readonly Mock<IMatrixUserLinkRepository> _userLinkRepo = new();
    private readonly Mock<ISubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<IUserCreditsRepository> _userCreditsRepo = new();
    private readonly Mock<IRateLimitService> _rateLimitService = new();
    private readonly Mock<IAgentThreadManager> _threadManager = new();
    private readonly Mock<IChatCompletionPort> _chatCompletion = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ILogger<SendMatrixMessageHandler>> _logger = new();

    private const string TestMatrixUserId = "@pablo:matrix.orchid-labs.xyz";
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testAgentId = Guid.NewGuid();
    private const string TestRoomId = "!abc123:matrix.orchid-labs.xyz";
    private const string TestContent = "Hello, I need help";

    public SendMatrixMessageHandlerTests()
    {
        _userCreditsRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        _handler = new SendMatrixMessageHandler(
            _userLinkRepo.Object,
            _subscriptionRepo.Object,
            _userCreditsRepo.Object,
            _rateLimitService.Object,
            _threadManager.Object,
            _chatCompletion.Object,
            _mediator.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsUserNotLinked_WhenUserNotFound()
    {
        var command = CreateCommand();

        _userLinkRepo.Setup(r => r.GetByMatrixUserIdWithUserAsync(TestMatrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("USER_NOT_LINKED", result.Error);
    }

    [Fact]
    public async Task Handle_ReturnsUserNotLinked_WhenLinkIsInactive()
    {
        var command = CreateCommand();

        var inactiveLink = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            MatrixUserId = TestMatrixUserId,
            IsActive = false
        };

        _userLinkRepo.Setup(r => r.GetByMatrixUserIdWithUserAsync(TestMatrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveLink);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("USER_NOT_LINKED", result.Error);
    }

    [Fact]
    public async Task Handle_ReturnsRateLimited_WhenRateLimitExceeded()
    {
        var command = CreateCommand();
        SetupLinkedUser();

        _rateLimitService.Setup(r => r.CheckAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Exceeded(10, 10, DateTime.UtcNow.AddSeconds(30)));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("RATE_LIMITED", result.Error);
        Assert.NotNull(result.RetryAfter);
    }

    [Fact]
    public async Task Handle_ReturnsInsufficientCredits_WhenNoSubscription()
    {
        var command = CreateCommand();
        SetupLinkedUser();
        SetupRateLimitAllowed();

        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INSUFFICIENT_CREDITS", result.Error);
        Assert.Equal(0, result.CreditsRemaining);
    }

    [Fact]
    public async Task Handle_ReturnsInsufficientCredits_WhenNotEnoughCredits()
    {
        var command = CreateCommand();
        SetupLinkedUser();
        SetupRateLimitAllowed();

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
        };

        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserCreditsEntity
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                AvailableCredits = 0,
                TotalCreditsGranted = 0,
                TotalCreditsConsumed = 0
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INSUFFICIENT_CREDITS", result.Error);
        Assert.Equal(0, result.CreditsRemaining);
    }

    [Fact]
    public async Task Handle_ReturnsAgentAccessDenied_WhenNoAccess()
    {
        var command = CreateCommand();
        SetupLinkedUser();
        SetupRateLimitAllowed();
        SetupSubscriptionWithCredits(100);

        _mediator.Setup(m => m.Send(
                It.IsAny<VerifyAgentAccessQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyAgentAccessDto(false, "User does not own this agent"));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("AGENT_ACCESS_DENIED", result.Error);
    }

    [Fact]
    public async Task Handle_ReturnsAgentNotFound_WhenAgentDoesNotExist()
    {
        var command = CreateCommand();
        SetupLinkedUser();
        SetupRateLimitAllowed();
        SetupSubscriptionWithCredits(100);
        SetupAgentAccessAllowed();
        SetupThreadManager();

        _chatCompletion.Setup(c => c.CompleteWithAgentAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                _testAgentId,
                _testUserId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AgentNotFoundException(_testAgentId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("AGENT_NOT_FOUND", result.Error);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenMessageProcessedSuccessfully()
    {
        var command = CreateCommand();
        SetupLinkedUser();
        SetupRateLimitAllowed();
        SetupSubscriptionWithCredits(100);
        SetupAgentAccessAllowed();
        SetupThreadManager();

        const string expectedResponse = "Hello! How can I help you today?";
        _chatCompletion.Setup(c => c.CompleteWithAgentAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                _testAgentId,
                _testUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        _mediator.Setup(m => m.Send(
                It.IsAny<ConsumeCreditsCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionDto { Credits = 99 });

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedResponse, result.Response);
        Assert.NotNull(result.SessionId);
        Assert.Equal(1, result.CreditsUsed);
        Assert.Equal(99, result.CreditsRemaining);
    }

    [Fact]
    public async Task Handle_ReturnsProcessingError_WhenContentIsEmpty()
    {
        var command = new SendMatrixMessageCommand(
            TestMatrixUserId,
            _testAgentId,
            TestRoomId,
            "");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("PROCESSING_ERROR", result.Error);
        Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_ReturnsProcessingError_WhenContentIsWhitespace()
    {
        var command = new SendMatrixMessageCommand(
            TestMatrixUserId,
            _testAgentId,
            TestRoomId,
            "   ");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("PROCESSING_ERROR", result.Error);
    }

    [Fact]
    public async Task Handle_ReturnsProcessingError_WhenContentExceedsMaxLength()
    {
        var longContent = new string('a', 10001);
        var command = new SendMatrixMessageCommand(
            TestMatrixUserId,
            _testAgentId,
            TestRoomId,
            longContent);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("PROCESSING_ERROR", result.Error);
        Assert.Contains("maximum length", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_GeneratesDeterministicSessionId_ForSameRoomAndAgent()
    {
        var command = CreateCommand();
        SetupLinkedUser();
        SetupRateLimitAllowed();
        SetupSubscriptionWithCredits(100);
        SetupAgentAccessAllowed();
        SetupThreadManager();

        _chatCompletion.Setup(c => c.CompleteWithAgentAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                _testAgentId,
                _testUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response");

        _mediator.Setup(m => m.Send(
                It.IsAny<ConsumeCreditsCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionDto { Credits = 99 });

        var result1 = await _handler.Handle(command, CancellationToken.None);
        var result2 = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(result1.SessionId, result2.SessionId);
        Assert.StartsWith("matrix_", result1.SessionId);
    }

    [Fact]
    public async Task Handle_AddsMessageToThread_BeforeProcessing()
    {
        var command = CreateCommand();
        SetupLinkedUser();
        SetupRateLimitAllowed();
        SetupSubscriptionWithCredits(100);
        SetupAgentAccessAllowed();
        SetupThreadManager();

        _chatCompletion.Setup(c => c.CompleteWithAgentAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                _testAgentId,
                _testUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response");

        _mediator.Setup(m => m.Send(
                It.IsAny<ConsumeCreditsCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionDto { Credits = 99 });

        await _handler.Handle(command, CancellationToken.None);

        _threadManager.Verify(t => t.AddMessageAsync(
            It.IsAny<string>(),
            "user",
            TestContent,
            It.IsAny<CancellationToken>()), Times.Once);

        _threadManager.Verify(t => t.AddMessageAsync(
            It.IsAny<string>(),
            "assistant",
            "Response",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private SendMatrixMessageCommand CreateCommand() => new(
        TestMatrixUserId,
        _testAgentId,
        TestRoomId,
        TestContent);

    private void SetupLinkedUser()
    {
        var user = new UserEntity { Id = _testUserId, Name = "Pablo", Email = "pablo@example.com" };
        var link = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            MatrixUserId = TestMatrixUserId,
            IsActive = true
        };
        typeof(MatrixUserLinkEntity).GetProperty("User")!.SetValue(link, user);

        _userLinkRepo.Setup(r => r.GetByMatrixUserIdWithUserAsync(TestMatrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);
    }

    private void SetupRateLimitAllowed()
    {
        _rateLimitService.Setup(r => r.CheckAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Allowed(1, 10, DateTime.UtcNow.AddMinutes(1)));
    }

    private void SetupSubscriptionWithCredits(int credits)
    {
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId
        };

        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _userCreditsRepo.Setup(r => r.GetByUserIdAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserCreditsEntity
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                AvailableCredits = credits,
                TotalCreditsGranted = credits,
                TotalCreditsConsumed = 0
            });
    }

    private void SetupAgentAccessAllowed()
    {
        _mediator.Setup(m => m.Send(
                It.IsAny<VerifyAgentAccessQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyAgentAccessDto(true));
    }

    private void SetupThreadManager()
    {
        _threadManager.Setup(t => t.GetOrCreateThreadAsync(
                It.IsAny<string>(),
                _testAgentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentThreadInfo("test-session", 0, false, DateTime.UtcNow));

        _threadManager.Setup(t => t.GetHistoryAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessage>());
    }
}
