using System.Security.Claims;
using Core.Application.UseCases.Dashboard.Common;
using Core.Application.UseCases.Dashboard.GetCreditHistory;
using Core.Application.UseCases.Dashboard.GetDashboardOverview;
using Core.Application.UseCases.Dashboard.GetSecurityMetrics;
using Core.Application.UseCases.Dashboard.GetSessionDetails;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class DashboardControllerTests
{
    private readonly DashboardController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly IConfiguration _configuration;

    public DashboardControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();

        // Use real ConfigurationBuilder instead of mocking
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Disable"] = "false",
                ["Auth:DevUserId"] = ""
            })
            .Build();

        _controller = new DashboardController(_mediatorMock.Object, _configuration);
    }

    #region Helper Methods

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private void SetupUnauthenticatedUser()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };
    }

    private void SetupUserWithInvalidGuid()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "not-a-valid-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetDashboardOverview_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetDashboardOverview();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetDashboardOverview_ThrowsUnauthorizedAccessException_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var act = () => _controller.GetDashboardOverview();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetCreditHistory_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetCreditHistory();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetSessionDetails_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetSessionDetails();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetSecurityMetrics_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetSecurityMetrics();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetCreditStatistics_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetCreditStatistics();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetSessionActivityHeatmap_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetSessionActivityHeatmap();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetAgentPerformance_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetAgentPerformance();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetPluginAnalytics_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetPluginAnalytics();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetWorkflowAnalytics_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetWorkflowAnalytics();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetKnowledgeBaseStatistics_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetKnowledgeBaseStatistics();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetMediaCenterStatistics_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetMediaCenterStatistics();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetBillingHistory_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetBillingHistory();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task GetUsageForecast_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.GetUsageForecast();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    [Fact]
    public async Task ExportDashboardData_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.ExportDashboardData();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID not found in claims");
    }

    #endregion

    #region GetDashboardOverview Tests

    [Fact]
    public async Task GetDashboardOverview_ReturnsOk_WithDashboardOverviewDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedDto = new DashboardOverviewDto
        {
            Credits = new CreditMetricsDto
            {
                CurrentBalance = 1000,
                ConsumedToday = 50,
                ConsumedThisWeek = 250,
                ConsumedThisMonth = 800,
                AverageDailyConsumption = 30.5m,
                DaysUntilExhaustion = 32,
                LowCreditAlert = false,
                AlertThreshold = 100
            },
            Subscription = new SubscriptionMetricsDto
            {
                PlanName = "Pro",
                IncludedCredits = 5000,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                DaysUntilExpiration = 30,
                AutoRenew = true,
                Price = 29.99m,
                PaymentUnit = "month"
            },
            Activity = new ActivityMetricsDto
            {
                TotalChatSessions = 150,
                ActiveSessions = 10,
                ArchivedSessions = 140,
                AverageMessagesPerSession = 25.5m,
                AverageSessionDuration = TimeSpan.FromMinutes(15)
            },
            Agents = new AgentMetricsDto
            {
                TotalAgents = 5,
                ActiveAgents = 4,
                DeletedAgents = 1
            },
            Usage = new UsageStatsDto
            {
                Plugins = new PluginUsageDto
                {
                    TotalPurchased = 10,
                    ActivePlugins = 8
                },
                Workflows = new WorkflowUsageDto
                {
                    TotalPurchased = 5,
                    ExecutedThisMonth = 100,
                    SuccessRate = 95.5m
                },
                KnowledgeBase = new KnowledgeBaseUsageDto
                {
                    TotalDocuments = 50,
                    TotalSizeBytes = 10485760,
                    IndexedDocuments = 48
                },
                MediaCenter = new MediaCenterUsageDto
                {
                    TotalAssets = 25,
                    TotalSizeBytes = 524288000,
                    TotalDuration = TimeSpan.FromHours(5)
                }
            }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetDashboardOverviewQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetDashboardOverview();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DashboardOverviewDto>().Subject;
        dto.Credits.CurrentBalance.Should().Be(1000);
        dto.Subscription.PlanName.Should().Be("Pro");
        dto.Activity.TotalChatSessions.Should().Be(150);
        dto.Agents.TotalAgents.Should().Be(5);
        dto.Usage.Plugins.TotalPurchased.Should().Be(10);
    }

    [Fact]
    public async Task GetDashboardOverview_PassesCorrectUserId_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedDto = new DashboardOverviewDto
        {
            Credits = new CreditMetricsDto(),
            Subscription = new SubscriptionMetricsDto(),
            Activity = new ActivityMetricsDto(),
            Agents = new AgentMetricsDto(),
            Usage = new UsageStatsDto
            {
                Plugins = new PluginUsageDto(),
                Workflows = new WorkflowUsageDto(),
                KnowledgeBase = new KnowledgeBaseUsageDto(),
                MediaCenter = new MediaCenterUsageDto()
            }
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetDashboardOverviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _controller.GetDashboardOverview();

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetDashboardOverviewQuery>(q => q.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetCreditHistory Tests

    [Fact]
    public async Task GetCreditHistory_ReturnsOk_WithCreditHistoryDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedDto = new CreditHistoryDto
        {
            Items = new List<CreditHistoryItemDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ConsumptionType = "chat",
                    ResourceName = "Agent Alpha",
                    CreditsConsumed = 10,
                    BalanceAfter = 990,
                    ConsumedAt = DateTime.UtcNow.AddHours(-1)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ConsumptionType = "plugin",
                    ResourceName = "Web Search",
                    CreditsConsumed = 5,
                    BalanceAfter = 985,
                    ConsumedAt = DateTime.UtcNow.AddHours(-2)
                }
            },
            TotalPages = 5,
            CurrentPage = 1
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetCreditHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetCreditHistory();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CreditHistoryDto>().Subject;
        dto.Items.Should().HaveCount(2);
        dto.TotalPages.Should().Be(5);
        dto.CurrentPage.Should().Be(1);
    }

    [Fact]
    public async Task GetCreditHistory_PassesDefaultParameters_WhenNotProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedDto = new CreditHistoryDto { Items = new List<CreditHistoryItemDto>() };
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetCreditHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _controller.GetCreditHistory();

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetCreditHistoryQuery>(q =>
                q.UserId == userId &&
                q.PageNumber == 1 &&
                q.PageSize == 20 &&
                q.StartDate == null &&
                q.EndDate == null &&
                q.ConsumptionType == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCreditHistory_PassesCustomParameters_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        const int pageNumber = 2;
        const int pageSize = 50;
        const string consumptionType = "chat";

        var expectedDto = new CreditHistoryDto { Items = new List<CreditHistoryItemDto>() };
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetCreditHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _controller.GetCreditHistory(pageNumber, pageSize, startDate, endDate, consumptionType);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetCreditHistoryQuery>(q =>
                q.UserId == userId &&
                q.PageNumber == pageNumber &&
                q.PageSize == pageSize &&
                q.StartDate == startDate &&
                q.EndDate == endDate &&
                q.ConsumptionType == consumptionType),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCreditHistory_ReturnsEmptyList_WhenNoHistory()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedDto = new CreditHistoryDto
        {
            Items = new List<CreditHistoryItemDto>(),
            TotalPages = 0,
            CurrentPage = 1
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetCreditHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetCreditHistory();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CreditHistoryDto>().Subject;
        dto.Items.Should().BeEmpty();
        dto.TotalPages.Should().Be(0);
    }

    #endregion

    #region GetCreditStatistics Tests (Pending Implementation)

    [Fact]
    public async Task GetCreditStatistics_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetCreditStatistics();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCreditStatistics_AcceptsDateRangeParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _controller.GetCreditStatistics(startDate, endDate);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetSessionDetails Tests

    [Fact]
    public async Task GetSessionDetails_ReturnsOk_WithSessionDetailsList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedSessions = new List<SessionDetailsDto>
        {
            new()
            {
                SessionId = Guid.NewGuid(),
                Title = "Chat with Agent Alpha",
                AgentId = Guid.NewGuid(),
                AgentName = "Agent Alpha",
                MessageCount = 25,
                CreditsConsumed = 50,
                Duration = TimeSpan.FromMinutes(15),
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                LastMessageAt = DateTime.UtcNow.AddDays(-1).AddMinutes(15),
                IsArchived = false
            },
            new()
            {
                SessionId = Guid.NewGuid(),
                Title = "Support Session",
                TeamId = Guid.NewGuid(),
                TeamName = "Support Team",
                MessageCount = 10,
                CreditsConsumed = 20,
                Duration = TimeSpan.FromMinutes(5),
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                LastMessageAt = DateTime.UtcNow.AddDays(-2).AddMinutes(5),
                IsArchived = true
            }
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetSessionDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSessions);

        // Act
        var result = await _controller.GetSessionDetails();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var sessions = okResult.Value.Should().BeAssignableTo<List<SessionDetailsDto>>().Subject;
        sessions.Should().HaveCount(2);
        sessions[0].Title.Should().Be("Chat with Agent Alpha");
        sessions[1].IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetSessionDetails_PassesDefaultParameters_WhenNotProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetSessionDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionDetailsDto>());

        // Act
        await _controller.GetSessionDetails();

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetSessionDetailsQuery>(q =>
                q.UserId == userId &&
                q.PageNumber == 1 &&
                q.PageSize == 20 &&
                q.IsArchived == null &&
                q.AgentId == null &&
                q.TeamId == null &&
                q.StartDate == null &&
                q.EndDate == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSessionDetails_PassesAllFilterParameters_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetSessionDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionDetailsDto>());

        // Act
        await _controller.GetSessionDetails(
            pageNumber: 3,
            pageSize: 50,
            isArchived: true,
            agentId: agentId,
            teamId: teamId,
            startDate: startDate,
            endDate: endDate);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetSessionDetailsQuery>(q =>
                q.UserId == userId &&
                q.PageNumber == 3 &&
                q.PageSize == 50 &&
                q.IsArchived == true &&
                q.AgentId == agentId &&
                q.TeamId == teamId &&
                q.StartDate == startDate &&
                q.EndDate == endDate),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSessionDetails_ReturnsEmptyList_WhenNoSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetSessionDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionDetailsDto>());

        // Act
        var result = await _controller.GetSessionDetails();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var sessions = okResult.Value.Should().BeAssignableTo<List<SessionDetailsDto>>().Subject;
        sessions.Should().BeEmpty();
    }

    #endregion

    #region GetSessionActivityHeatmap Tests (Pending Implementation)

    [Fact]
    public async Task GetSessionActivityHeatmap_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetSessionActivityHeatmap();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSessionActivityHeatmap_AcceptsDateRangeParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _controller.GetSessionActivityHeatmap(startDate, endDate);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetAgentPerformance Tests (Pending Implementation)

    [Fact]
    public async Task GetAgentPerformance_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetAgentPerformance();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAgentPerformance_AcceptsDateRangeParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _controller.GetAgentPerformance(startDate, endDate);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetPluginAnalytics Tests (Pending Implementation)

    [Fact]
    public async Task GetPluginAnalytics_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetPluginAnalytics();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetWorkflowAnalytics Tests (Pending Implementation)

    [Fact]
    public async Task GetWorkflowAnalytics_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetWorkflowAnalytics();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetKnowledgeBaseStatistics Tests (Pending Implementation)

    [Fact]
    public async Task GetKnowledgeBaseStatistics_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetKnowledgeBaseStatistics();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetMediaCenterStatistics Tests (Pending Implementation)

    [Fact]
    public async Task GetMediaCenterStatistics_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetMediaCenterStatistics();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetSecurityMetrics Tests

    [Fact]
    public async Task GetSecurityMetrics_ReturnsOk_WithSecurityMetricsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedDto = new SecurityMetricsDto
        {
            LastLogin = DateTime.UtcNow.AddHours(-2),
            RecentLogins = new List<LoginHistoryDto>
            {
                new()
                {
                    Timestamp = DateTime.UtcNow.AddHours(-2),
                    IpAddress = "192.168.1.100",
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    Location = "New York, US",
                    Success = true
                },
                new()
                {
                    Timestamp = DateTime.UtcNow.AddDays(-1),
                    IpAddress = "192.168.1.101",
                    UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_0)",
                    Location = "Los Angeles, US",
                    Success = true
                }
            },
            FailedLoginAttempts = 2,
            LastPasswordChange = DateTime.UtcNow.AddDays(-30),
            ActiveDevices = new List<string>
            {
                "Chrome on Windows",
                "Safari on iPhone"
            }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetSecurityMetricsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetSecurityMetrics();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<SecurityMetricsDto>().Subject;
        dto.FailedLoginAttempts.Should().Be(2);
        dto.RecentLogins.Should().HaveCount(2);
        dto.ActiveDevices.Should().HaveCount(2);
        dto.LastPasswordChange.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSecurityMetrics_PassesCorrectUserId_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedDto = new SecurityMetricsDto
        {
            RecentLogins = new List<LoginHistoryDto>(),
            ActiveDevices = new List<string>()
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetSecurityMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _controller.GetSecurityMetrics();

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetSecurityMetricsQuery>(q => q.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSecurityMetrics_ReturnsDto_WithNullableFieldsAsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedDto = new SecurityMetricsDto
        {
            LastLogin = null,
            RecentLogins = new List<LoginHistoryDto>(),
            FailedLoginAttempts = 0,
            LastPasswordChange = null,
            ActiveDevices = new List<string>()
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetSecurityMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetSecurityMetrics();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<SecurityMetricsDto>().Subject;
        dto.LastLogin.Should().BeNull();
        dto.LastPasswordChange.Should().BeNull();
        dto.FailedLoginAttempts.Should().Be(0);
    }

    #endregion

    #region GetBillingHistory Tests (Pending Implementation)

    [Fact]
    public async Task GetBillingHistory_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetBillingHistory();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBillingHistory_AcceptsPaginationParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetBillingHistory(pageNumber: 2, pageSize: 50);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetUsageForecast Tests (Pending Implementation)

    [Fact]
    public async Task GetUsageForecast_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetUsageForecast();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region ExportDashboardData Tests (Pending Implementation)

    [Fact]
    public async Task ExportDashboardData_ReturnsOk_WithPendingMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.ExportDashboardData();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExportDashboardData_AcceptsFormatParameter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.ExportDashboardData(format: "pdf");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExportDashboardData_AcceptsDateRangeParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _controller.ExportDashboardData("csv", startDate, endDate);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Response Shape Verification Tests

    [Fact]
    public async Task DashboardOverviewDto_HasCorrectStructure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var dto = new DashboardOverviewDto
        {
            Credits = new CreditMetricsDto
            {
                CurrentBalance = 500,
                ConsumedToday = 25,
                ConsumedThisWeek = 100,
                ConsumedThisMonth = 400,
                AverageDailyConsumption = 15m,
                DaysUntilExhaustion = 33,
                ConsumptionByType = new List<CreditConsumptionByTypeDto>
                {
                    new() { Type = "chat", CreditsConsumed = 300, Percentage = 75m }
                },
                TopConsumingResources = new List<TopResourceConsumptionDto>
                {
                    new()
                    {
                        ResourceId = Guid.NewGuid().ToString(),
                        ResourceName = "Agent Alpha",
                        ResourceType = "agent",
                        CreditsConsumed = 200
                    }
                },
                LowCreditAlert = false,
                AlertThreshold = 100
            },
            Subscription = new SubscriptionMetricsDto
            {
                PlanName = "Pro",
                IncludedCredits = 1000,
                ExpiresAt = DateTime.UtcNow.AddMonths(1),
                DaysUntilExpiration = 30,
                AutoRenew = true,
                Price = 29.99m,
                PaymentUnit = "month",
                RecommendedPlan = "Enterprise"
            },
            Activity = new ActivityMetricsDto
            {
                TotalChatSessions = 50,
                ActiveSessions = 5,
                ArchivedSessions = 45,
                AverageMessagesPerSession = 20m,
                AverageSessionDuration = TimeSpan.FromMinutes(10),
                SessionsByAgent = new List<SessionsByAgentDto>
                {
                    new()
                    {
                        AgentId = Guid.NewGuid(),
                        AgentName = "Alpha",
                        SessionCount = 30,
                        Percentage = 60m
                    }
                },
                SessionsByTeam = new List<SessionsByTeamDto>
                {
                    new()
                    {
                        TeamId = Guid.NewGuid(),
                        TeamName = "Support",
                        SessionCount = 20,
                        Percentage = 40m
                    }
                },
                ActivityByHour = new List<ActivityByHourDto>
                {
                    new() { Hour = 9, MessageCount = 100 }
                }
            },
            Agents = new AgentMetricsDto
            {
                TotalAgents = 3,
                ActiveAgents = 2,
                DeletedAgents = 1,
                TopAgents = new List<TopAgentDto>
                {
                    new()
                    {
                        AgentId = Guid.NewGuid(),
                        AgentName = "Alpha",
                        AvatarUrl = "https://example.com/avatar.png",
                        SessionCount = 30,
                        MessageCount = 600,
                        AverageResponseTime = 1.5m
                    }
                },
                LanguageDistribution = new List<AgentLanguageDistributionDto>
                {
                    new() { Language = "en", Count = 2, Percentage = 66.67m }
                }
            },
            Usage = new UsageStatsDto
            {
                Plugins = new PluginUsageDto
                {
                    TotalPurchased = 5,
                    ActivePlugins = 4,
                    TopPlugins = new List<TopPluginDto>
                    {
                        new()
                        {
                            PluginId = Guid.NewGuid(),
                            PluginName = "Web Search",
                            UsageCount = 50,
                            CreditsConsumed = 100
                        }
                    },
                    SubscriptionBasedCount = 2,
                    OneTimePurchaseCount = 3
                },
                Workflows = new WorkflowUsageDto
                {
                    TotalPurchased = 2,
                    ExecutedThisMonth = 25,
                    SuccessRate = 92m,
                    TopWorkflows = new List<TopWorkflowDto>
                    {
                        new()
                        {
                            WorkflowId = Guid.NewGuid(),
                            WorkflowName = "Data Processing",
                            ExecutionCount = 15,
                            SuccessRate = 95m
                        }
                    }
                },
                KnowledgeBase = new KnowledgeBaseUsageDto
                {
                    TotalDocuments = 20,
                    TotalSizeBytes = 5242880,
                    IndexedDocuments = 18,
                    FileTypeDistribution = new Dictionary<string, int>
                    {
                        { "pdf", 10 },
                        { "docx", 5 },
                        { "txt", 5 }
                    },
                    TopTags = new List<string> { "documentation", "api", "guide" }
                },
                MediaCenter = new MediaCenterUsageDto
                {
                    TotalAssets = 10,
                    TotalSizeBytes = 104857600,
                    TotalDuration = TimeSpan.FromHours(2),
                    MediaTypeDistribution = new Dictionary<string, int>
                    {
                        { "video", 5 },
                        { "audio", 5 }
                    }
                }
            }
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetDashboardOverviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _controller.GetDashboardOverview();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responseDto = okResult.Value.Should().BeOfType<DashboardOverviewDto>().Subject;

        // Verify nested structure
        responseDto.Credits.ConsumptionByType.Should().HaveCount(1);
        responseDto.Credits.TopConsumingResources.Should().HaveCount(1);
        responseDto.Activity.SessionsByAgent.Should().HaveCount(1);
        responseDto.Activity.SessionsByTeam.Should().HaveCount(1);
        responseDto.Activity.ActivityByHour.Should().HaveCount(1);
        responseDto.Agents.TopAgents.Should().HaveCount(1);
        responseDto.Agents.LanguageDistribution.Should().HaveCount(1);
        responseDto.Usage.Plugins.TopPlugins.Should().HaveCount(1);
        responseDto.Usage.Workflows.TopWorkflows.Should().HaveCount(1);
        responseDto.Usage.KnowledgeBase.FileTypeDistribution.Should().HaveCount(3);
        responseDto.Usage.KnowledgeBase.TopTags.Should().HaveCount(3);
        responseDto.Usage.MediaCenter.MediaTypeDistribution.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreditHistoryItemDto_HasCorrectStructure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var historyItemId = Guid.NewGuid();
        var consumedAt = DateTime.UtcNow.AddHours(-1);

        var expectedDto = new CreditHistoryDto
        {
            Items = new List<CreditHistoryItemDto>
            {
                new()
                {
                    Id = historyItemId,
                    ConsumptionType = "chat",
                    ResourceName = "Agent Alpha",
                    CreditsConsumed = 10,
                    BalanceAfter = 990,
                    ConsumedAt = consumedAt,
                    Metadata = new Dictionary<string, object>
                    {
                        { "sessionId", Guid.NewGuid().ToString() },
                        { "messageCount", 5 }
                    }
                }
            },
            TotalPages = 1,
            CurrentPage = 1
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetCreditHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetCreditHistory();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CreditHistoryDto>().Subject;

        var item = dto.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(historyItemId);
        item.ConsumptionType.Should().Be("chat");
        item.ResourceName.Should().Be("Agent Alpha");
        item.CreditsConsumed.Should().Be(10);
        item.BalanceAfter.Should().Be(990);
        item.ConsumedAt.Should().Be(consumedAt);
        item.Metadata.Should().NotBeNull();
        item.Metadata.Should().ContainKey("sessionId");
        item.Metadata.Should().ContainKey("messageCount");
    }

    [Fact]
    public async Task SessionDetailsDto_HasCorrectStructure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var sessionId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var lastMessageAt = DateTime.UtcNow.AddDays(-1).AddMinutes(30);

        var expectedSessions = new List<SessionDetailsDto>
        {
            new()
            {
                SessionId = sessionId,
                Title = "Technical Support",
                AgentId = agentId,
                AgentName = "Support Bot",
                TeamId = teamId,
                TeamName = "Tech Team",
                MessageCount = 50,
                CreditsConsumed = 100,
                Duration = TimeSpan.FromMinutes(30),
                CreatedAt = createdAt,
                LastMessageAt = lastMessageAt,
                IsArchived = false
            }
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetSessionDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSessions);

        // Act
        var result = await _controller.GetSessionDetails();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var sessions = okResult.Value.Should().BeAssignableTo<List<SessionDetailsDto>>().Subject;

        var session = sessions.Should().ContainSingle().Subject;
        session.SessionId.Should().Be(sessionId);
        session.Title.Should().Be("Technical Support");
        session.AgentId.Should().Be(agentId);
        session.AgentName.Should().Be("Support Bot");
        session.TeamId.Should().Be(teamId);
        session.TeamName.Should().Be("Tech Team");
        session.MessageCount.Should().Be(50);
        session.CreditsConsumed.Should().Be(100);
        session.Duration.Should().Be(TimeSpan.FromMinutes(30));
        session.CreatedAt.Should().Be(createdAt);
        session.LastMessageAt.Should().Be(lastMessageAt);
        session.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task SecurityMetricsDto_HasCorrectStructure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var lastLogin = DateTime.UtcNow.AddHours(-1);
        var lastPasswordChange = DateTime.UtcNow.AddDays(-60);
        var loginTimestamp = DateTime.UtcNow.AddHours(-1);

        var expectedDto = new SecurityMetricsDto
        {
            LastLogin = lastLogin,
            RecentLogins = new List<LoginHistoryDto>
            {
                new()
                {
                    Timestamp = loginTimestamp,
                    IpAddress = "10.0.0.1",
                    UserAgent = "Mozilla/5.0",
                    Location = "San Francisco, US",
                    Success = true
                }
            },
            FailedLoginAttempts = 3,
            LastPasswordChange = lastPasswordChange,
            ActiveDevices = new List<string> { "Chrome on macOS", "Safari on iOS" }
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetSecurityMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetSecurityMetrics();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<SecurityMetricsDto>().Subject;

        dto.LastLogin.Should().Be(lastLogin);
        dto.FailedLoginAttempts.Should().Be(3);
        dto.LastPasswordChange.Should().Be(lastPasswordChange);
        dto.ActiveDevices.Should().HaveCount(2);

        var login = dto.RecentLogins.Should().ContainSingle().Subject;
        login.Timestamp.Should().Be(loginTimestamp);
        login.IpAddress.Should().Be("10.0.0.1");
        login.UserAgent.Should().Be("Mozilla/5.0");
        login.Location.Should().Be("San Francisco, US");
        login.Success.Should().BeTrue();
    }

    #endregion
}
