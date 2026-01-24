using Application.UseCases.Team.Common;
using Application.UseCases.Team.CreateTeam;
using Application.UseCases.Team.ListTeams;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Moq;
using WebApi.Controllers;
using WebApi.Models;

namespace WebApi.Tests.Controllers;

public class TeamsControllerTests
{
    private readonly TeamsController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public TeamsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new TeamsController(_mediatorMock.Object);
    }

    #region GetTeams Tests

    [Fact]
    public async Task GetTeams_ReturnsOk_WithTeamsList()
    {
        // Arrange
        var teams = new List<TeamDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Team 1" },
            new() { Id = Guid.NewGuid(), Name = "Team 2" }
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ListTeamsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(teams);

        // Act
        var result = await _controller.GetTeams();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(teams, okResult.Value);
    }

    [Fact]
    public async Task GetTeams_ReturnsOk_WithEmptyList_WhenNoTeams()
    {
        // Arrange
        var teams = new List<TeamDto>();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ListTeamsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(teams);

        // Act
        var result = await _controller.GetTeams();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(teams, okResult.Value);
    }

    [Fact]
    public async Task GetTeams_CallsMediator_WithListTeamsQuery()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ListTeamsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamDto>());

        // Act
        await _controller.GetTeams();

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<ListTeamsQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CreateTeam Tests

    [Fact]
    public async Task CreateTeam_ReturnsCreatedAtAction_WithTeam()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var request = new CreateTeamRequest
        {
            Name = "New Team",
            Description = "Test Description",
            Policy = TeamInteractionPolicy.Open
        };

        var createdTeam = new TeamDto
        {
            Id = teamId,
            Name = request.Name,
            Description = request.Description
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateTeamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTeam);

        // Act
        var result = await _controller.CreateTeam(request);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(TeamsController.GetTeams), createdAtActionResult.ActionName);
        var routeValues = Assert.IsType<RouteValueDictionary>(createdAtActionResult.RouteValues);
        Assert.Equal(teamId, routeValues["id"]);
        Assert.Equal(createdTeam, createdAtActionResult.Value);
    }

    [Fact]
    public async Task CreateTeam_CallsMediator_WithCorrectCommand()
    {
        // Arrange
        var agents = new List<TeamAgentInput>
        {
            new(Guid.NewGuid(), "analyzer", 1)
        };

        var request = new CreateTeamRequest
        {
            Name = "Test Team",
            Description = "Description",
            Policy = TeamInteractionPolicy.LeaderDriven,
            Agents = agents
        };

        var createdTeam = new TeamDto { Id = Guid.NewGuid() };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateTeamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTeam);

        // Act
        await _controller.CreateTeam(request);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<CreateTeamCommand>(cmd =>
                    cmd.Name == request.Name &&
                    cmd.Description == request.Description &&
                    cmd.Policy == request.Policy &&
                    cmd.Agents == agents),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTeam_HandlesNullDescription()
    {
        // Arrange
        var request = new CreateTeamRequest
        {
            Name = "Team Without Description",
            Description = null,
            Policy = TeamInteractionPolicy.Open
        };

        var createdTeam = new TeamDto { Id = Guid.NewGuid() };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateTeamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTeam);

        // Act
        var result = await _controller.CreateTeam(request);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result);
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<CreateTeamCommand>(cmd => cmd.Description == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTeam_HandlesNullAgents()
    {
        // Arrange
        var request = new CreateTeamRequest
        {
            Name = "Team Without Agents",
            Policy = TeamInteractionPolicy.Open,
            Agents = null
        };

        var createdTeam = new TeamDto { Id = Guid.NewGuid() };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateTeamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTeam);

        // Act
        var result = await _controller.CreateTeam(request);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result);
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<CreateTeamCommand>(cmd => cmd.Agents == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTeam_SupportsOpenPolicy()
    {
        // Arrange
        var request = new CreateTeamRequest
        {
            Name = "Open Team",
            Policy = TeamInteractionPolicy.Open
        };

        var createdTeam = new TeamDto { Id = Guid.NewGuid() };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateTeamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTeam);

        // Act
        var result = await _controller.CreateTeam(request);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result);
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<CreateTeamCommand>(cmd => cmd.Policy == TeamInteractionPolicy.Open),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTeam_SupportsLeaderDrivenPolicy()
    {
        // Arrange
        var request = new CreateTeamRequest
        {
            Name = "Leader Driven Team",
            Policy = TeamInteractionPolicy.LeaderDriven
        };

        var createdTeam = new TeamDto { Id = Guid.NewGuid() };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateTeamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTeam);

        // Act
        var result = await _controller.CreateTeam(request);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result);
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<CreateTeamCommand>(cmd => cmd.Policy == TeamInteractionPolicy.LeaderDriven),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
