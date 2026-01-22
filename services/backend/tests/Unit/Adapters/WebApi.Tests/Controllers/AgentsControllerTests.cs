using System.Security.Claims;
using Application.UseCases.Agent.Common;
using Application.UseCases.Agent.CreateAgent;
using Application.UseCases.Agent.GetAgentRatings;
using Application.UseCases.Agent.ListAgents;
using Application.UseCases.Agent.ListRecycleBinAgents;
using Application.UseCases.Agent.PermanentDeleteAgent;
using Application.UseCases.Agent.RateAgent;
using Application.UseCases.Agent.RestoreAgent;
using Application.UseCases.Agent.SoftDeleteAgent;
using Application.UseCases.Agent.UpdateAgent;
using Application.UseCases.Agent.VerifyAgentAccess;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class AgentsControllerTests
{
    private readonly AgentsController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public AgentsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new AgentsController(_mediatorMock.Object);
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

    #region GetAgents Tests

    [Fact]
    public async Task GetAgents_ReturnsOk_WithAgentsList_WhenUnauthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var expectedAgents = new List<AgentDto>
        {
            new AgentDto { Id = Guid.NewGuid(), Name = "Agent 1", IsPublic = true },
            new AgentDto { Id = Guid.NewGuid(), Name = "Agent 2", IsPublic = true }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListAgentsQuery>(q => q.UserId == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAgents);

        // Act
        var result = await _controller.GetAgents();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var agents = okResult.Value.Should().BeAssignableTo<List<AgentDto>>().Subject;
        agents.Should().HaveCount(2);
        agents[0].Name.Should().Be("Agent 1");
    }

    [Fact]
    public async Task GetAgents_ReturnsOk_WithAgentsList_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var expectedAgents = new List<AgentDto>
        {
            new AgentDto { Id = Guid.NewGuid(), Name = "Agent 1", IsPublic = false },
            new AgentDto { Id = Guid.NewGuid(), Name = "Agent 2", IsPublic = true }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListAgentsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAgents);

        // Act
        var result = await _controller.GetAgents();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var agents = okResult.Value.Should().BeAssignableTo<List<AgentDto>>().Subject;
        agents.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAgents_SendsCorrectQuery_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        
        _mediatorMock.Setup(m => m.Send(It.IsAny<ListAgentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDto>());

        // Act
        await _controller.GetAgents();

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ListAgentsQuery>(q => q.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetMyAgents Tests

    [Fact]
    public async Task GetMyAgents_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetMyAgents();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task GetMyAgents_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GetMyAgents();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task GetMyAgents_ReturnsOk_WithUserAgents_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var expectedAgents = new List<AgentDto>
        {
            new AgentDto { Id = Guid.NewGuid(), Name = "My Agent 1", UserId = userId },
            new AgentDto { Id = Guid.NewGuid(), Name = "My Agent 2", UserId = userId }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListAgentsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAgents);

        // Act
        var result = await _controller.GetMyAgents();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var agents = okResult.Value.Should().BeAssignableTo<List<AgentDto>>().Subject;
        agents.Should().HaveCount(2);
        agents.All(a => a.UserId == userId).Should().BeTrue();
    }

    #endregion

    #region CreateAgent Tests

    [Fact]
    public async Task CreateAgent_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var request = new CreateAgentRequest { Name = "Test Agent" };

        // Act
        var result = await _controller.CreateAgent(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task CreateAgent_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var request = new CreateAgentRequest { Name = "Test Agent" };

        // Act
        var result = await _controller.CreateAgent(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task CreateAgent_ReturnsCreatedAtAction_WithAgent_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new CreateAgentRequest
        {
            Name = "Test Agent",
            Description = "Test Description",
            AvatarUrl = "https://example.com/avatar.png",
            Personality = "Helpful and friendly",
            Language = "en",
            Model = "gpt-4",
            IsPublic = true
        };

        var expectedAgent = new AgentDto
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            AvatarUrl = request.AvatarUrl,
            UserId = userId
        };

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<CreateAgentCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAgent);

        // Act
        var result = await _controller.CreateAgent(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AgentsController.GetAgents));
        var agent = createdResult.Value.Should().BeOfType<AgentDto>().Subject;
        agent.Name.Should().Be(request.Name);
        agent.Description.Should().Be(request.Description);
    }

    [Fact]
    public async Task CreateAgent_SendsCorrectCommand_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var pluginIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var personalityTemplateId = Guid.NewGuid();
        var request = new CreateAgentRequest
        {
            Name = "Test Agent",
            Description = "Description",
            AvatarUrl = "https://example.com/avatar.png",
            Personality = "Personality text",
            PersonalityTemplateId = personalityTemplateId,
            Language = "es",
            Model = "gpt-3.5-turbo",
            PluginIds = pluginIds,
            IsPublic = false
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateAgentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentDto { Id = Guid.NewGuid(), Name = request.Name });

        // Act
        await _controller.CreateAgent(request);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<CreateAgentCommand>(cmd =>
                cmd.Name == request.Name &&
                cmd.Description == request.Description &&
                cmd.AvatarUrl == request.AvatarUrl &&
                cmd.Personality == request.Personality &&
                cmd.PersonalityTemplateId == personalityTemplateId &&
                cmd.Language == request.Language &&
                cmd.Model == request.Model &&
                cmd.PluginIds == pluginIds &&
                cmd.UserId == userId &&
                cmd.IsPublic == request.IsPublic),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateAgent Tests

    [Fact]
    public async Task UpdateAgent_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var agentId = Guid.NewGuid();
        var request = new UpdateAgentRequest { Name = "Updated Name" };

        // Act
        var result = await _controller.UpdateAgent(agentId, request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task UpdateAgent_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var agentId = Guid.NewGuid();
        var request = new UpdateAgentRequest { Name = "Updated Name" };

        // Act
        var result = await _controller.UpdateAgent(agentId, request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task UpdateAgent_ReturnsOk_WithUpdatedAgent_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new UpdateAgentRequest
        {
            Name = "Updated Name",
            Description = "Updated Description",
            IsPublic = true
        };

        var expectedAgent = new AgentDto
        {
            Id = agentId,
            Name = request.Name ?? string.Empty,
            Description = request.Description,
            IsPublic = request.IsPublic ?? false
        };

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<UpdateAgentCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAgent);

        // Act
        var result = await _controller.UpdateAgent(agentId, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var agent = okResult.Value.Should().BeOfType<AgentDto>().Subject;
        agent.Id.Should().Be(agentId);
        agent.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task UpdateAgent_SendsCorrectCommand_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pluginIds = new[] { Guid.NewGuid() };
        var personalityTemplateId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new UpdateAgentRequest
        {
            Name = "New Name",
            Description = "New Description",
            AvatarUrl = "https://example.com/new-avatar.png",
            Personality = "New personality",
            PersonalityTemplateId = personalityTemplateId,
            Language = "fr",
            Model = "gpt-4",
            PluginIds = pluginIds,
            IsPublic = false
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateAgentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentDto { Id = agentId });

        // Act
        await _controller.UpdateAgent(agentId, request);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<UpdateAgentCommand>(cmd =>
                cmd.AgentId == agentId &&
                cmd.UserId == userId &&
                cmd.Name == request.Name &&
                cmd.Description == request.Description &&
                cmd.AvatarUrl == request.AvatarUrl &&
                cmd.Personality == request.Personality &&
                cmd.PersonalityTemplateId == personalityTemplateId &&
                cmd.Language == request.Language &&
                cmd.Model == request.Model &&
                cmd.PluginIds == pluginIds &&
                cmd.IsPublic == request.IsPublic),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SoftDeleteAgent Tests

    [Fact]
    public async Task SoftDeleteAgent_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var agentId = Guid.NewGuid();

        // Act
        var result = await _controller.SoftDeleteAgent(agentId);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task SoftDeleteAgent_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var agentId = Guid.NewGuid();

        // Act
        var result = await _controller.SoftDeleteAgent(agentId);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task SoftDeleteAgent_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<SoftDeleteAgentCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        // Act
        var result = await _controller.SoftDeleteAgent(agentId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SoftDeleteAgent_SendsCorrectCommand_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<SoftDeleteAgentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        // Act
        await _controller.SoftDeleteAgent(agentId);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<SoftDeleteAgentCommand>(cmd =>
                cmd.AgentId == agentId &&
                cmd.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetRecycleBinAgents Tests

    [Fact]
    public async Task GetRecycleBinAgents_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetRecycleBinAgents();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task GetRecycleBinAgents_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GetRecycleBinAgents();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task GetRecycleBinAgents_ReturnsOk_WithDeletedAgents_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var expectedAgents = new List<AgentDto>
        {
            new AgentDto { Id = Guid.NewGuid(), Name = "Deleted Agent 1" },
            new AgentDto { Id = Guid.NewGuid(), Name = "Deleted Agent 2" }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListRecycleBinAgentsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAgents);

        // Act
        var result = await _controller.GetRecycleBinAgents();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var agents = okResult.Value.Should().BeAssignableTo<List<AgentDto>>().Subject;
        agents.Should().HaveCount(2);
    }

    #endregion

    #region RestoreAgent Tests

    [Fact]
    public async Task RestoreAgent_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var agentId = Guid.NewGuid();

        // Act
        var result = await _controller.RestoreAgent(agentId);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task RestoreAgent_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var agentId = Guid.NewGuid();

        // Act
        var result = await _controller.RestoreAgent(agentId);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task RestoreAgent_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<RestoreAgentCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        // Act
        var result = await _controller.RestoreAgent(agentId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RestoreAgent_SendsCorrectCommand_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<RestoreAgentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        // Act
        await _controller.RestoreAgent(agentId);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<RestoreAgentCommand>(cmd =>
                cmd.AgentId == agentId &&
                cmd.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region PermanentDeleteAgent Tests

    [Fact]
    public async Task PermanentDeleteAgent_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var agentId = Guid.NewGuid();

        // Act
        var result = await _controller.PermanentDeleteAgent(agentId);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task PermanentDeleteAgent_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var agentId = Guid.NewGuid();

        // Act
        var result = await _controller.PermanentDeleteAgent(agentId);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task PermanentDeleteAgent_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<PermanentDeleteAgentCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        // Act
        var result = await _controller.PermanentDeleteAgent(agentId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task PermanentDeleteAgent_SendsCorrectCommand_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<PermanentDeleteAgentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        // Act
        await _controller.PermanentDeleteAgent(agentId);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<PermanentDeleteAgentCommand>(cmd =>
                cmd.AgentId == agentId &&
                cmd.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region RateAgent Tests

    [Fact]
    public async Task RateAgent_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var agentId = Guid.NewGuid();
        var request = new RateAgentRequest { Rating = 5, Comment = "Great!" };

        // Act
        var result = await _controller.RateAgent(agentId, request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task RateAgent_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var agentId = Guid.NewGuid();
        var request = new RateAgentRequest { Rating = 5, Comment = "Great!" };

        // Act
        var result = await _controller.RateAgent(agentId, request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task RateAgent_ReturnsOk_WithRatingResult_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new RateAgentRequest { Rating = 4, Comment = "Very good" };

        var expectedResult = new AgentRatingDto
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            UserId = userId,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<RateAgentCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.RateAgent(agentId, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var ratingResult = okResult.Value.Should().BeOfType<AgentRatingDto>().Subject;
        ratingResult.Rating.Should().Be(request.Rating);
        ratingResult.Comment.Should().Be(request.Comment);
    }

    [Fact]
    public async Task RateAgent_SendsCorrectCommand_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new RateAgentRequest { Rating = 3, Comment = "Average" };

        _mediatorMock.Setup(m => m.Send(It.IsAny<RateAgentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRatingDto { Id = Guid.NewGuid(), AgentId = agentId, UserId = userId, Rating = 3 });

        // Act
        await _controller.RateAgent(agentId, request);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<RateAgentCommand>(cmd =>
                cmd.AgentId == agentId &&
                cmd.UserId == userId &&
                cmd.Rating == request.Rating &&
                cmd.Comment == request.Comment),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetAgentRatings Tests

    [Fact]
    public async Task GetAgentRatings_ReturnsOk_WithRatings_WhenSuccessful()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var expectedRatings = new AgentRatingsResultDto
        {
            AgentId = agentId,
            AverageRating = 4.5,
            TotalRatings = 100,
            Ratings = new List<AgentRatingDto>
            {
                new AgentRatingDto { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), AgentId = agentId, Rating = 5, Comment = "Excellent!" },
                new AgentRatingDto { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), AgentId = agentId, Rating = 4, Comment = "Good" }
            }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetAgentRatingsQuery>(q => q.AgentId == agentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRatings);

        // Act
        var result = await _controller.GetAgentRatings(agentId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var ratings = okResult.Value.Should().BeOfType<AgentRatingsResultDto>().Subject;
        ratings.AgentId.Should().Be(agentId);
        ratings.AverageRating.Should().Be(4.5);
        ratings.TotalRatings.Should().Be(100);
    }

    [Fact]
    public async Task GetAgentRatings_SendsCorrectQuery_ToMediator()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetAgentRatingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRatingsResultDto { AgentId = agentId });

        // Act
        await _controller.GetAgentRatings(agentId);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetAgentRatingsQuery>(q => q.AgentId == agentId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region VerifyAgentAccess Tests

    [Fact]
    public async Task VerifyAgentAccess_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var agentId = Guid.NewGuid();

        // Act
        var result = await _controller.VerifyAgentAccess(agentId);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task VerifyAgentAccess_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var agentId = Guid.NewGuid();

        // Act
        var result = await _controller.VerifyAgentAccess(agentId);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().Be("Invalid user identity");
    }

    [Fact]
    public async Task VerifyAgentAccess_ReturnsOk_WhenUserHasAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedResult = new VerifyAgentAccessResult(true, "User owns the agent");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<VerifyAgentAccessQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.VerifyAgentAccess(agentId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var accessResult = okResult.Value.Should().BeOfType<VerifyAgentAccessResult>().Subject;
        accessResult.HasAccess.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAgentAccess_ReturnsForbidden_WhenUserDoesNotHaveAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedResult = new VerifyAgentAccessResult(false, "Agent is private");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<VerifyAgentAccessQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.VerifyAgentAccess(agentId);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(403);
        var accessResult = statusCodeResult.Value.Should().BeOfType<VerifyAgentAccessResult>().Subject;
        accessResult.HasAccess.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAgentAccess_SendsCorrectQuery_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<VerifyAgentAccessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyAgentAccessResult(true));

        // Act
        await _controller.VerifyAgentAccess(agentId);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<VerifyAgentAccessQuery>(q =>
                q.UserId == userId &&
                q.AgentId == agentId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
