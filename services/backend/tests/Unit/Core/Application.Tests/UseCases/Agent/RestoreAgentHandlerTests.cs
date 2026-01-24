using Application.Interfaces;
using Application.UseCases.Agent.RestoreAgent;
using Domain.Entities;
using Domain.Exceptions;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Agent;

public class RestoreAgentHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly RestoreAgentHandler _handler;

    public RestoreAgentHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _handler = new RestoreAgentHandler(_agentRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Restore_Agent_From_RecycleBin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            IsDeleted = true,
            IsInRecycleBin = true,
            DeletedAt = DateTime.UtcNow.AddDays(-5),
            RecycleBinExpiresAt = DateTime.UtcNow.AddDays(25)
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RestoreAgentCommand(agentId, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(agent.IsDeleted);
        Assert.False(agent.IsInRecycleBin);
        Assert.Null(agent.DeletedAt);
        Assert.Null(agent.RecycleBinExpiresAt);

        _agentRepositoryMock.Verify(x => x.UpdateAsync(agent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Throw_InvalidOperationException_When_Agent_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        var command = new RestoreAgentCommand(agentId, userId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AgentNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(agentId.ToString(), exception.Message);

        _agentRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Throw_UnauthorizedDomainAccessException_When_User_Is_Not_Owner()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var attemptingUserId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = ownerId,
            IsDeleted = true,
            IsInRecycleBin = true
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new RestoreAgentCommand(agentId, attemptingUserId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedDomainAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        _agentRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Throw_AgentNotInRecycleBinException_When_Agent_Not_In_RecycleBin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            IsDeleted = false,
            IsInRecycleBin = false // Not in recycle bin
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new RestoreAgentCommand(agentId, userId);

        // Act & Assert
        await Assert.ThrowsAsync<AgentNotInRecycleBinException>(
            () => _handler.Handle(command, CancellationToken.None));

        _agentRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Update_UpdatedAt_Timestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var originalUpdatedAt = DateTime.UtcNow.AddDays(-10);

        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            IsDeleted = true,
            IsInRecycleBin = true,
            UpdatedAt = originalUpdatedAt
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RestoreAgentCommand(agentId, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(agent.UpdatedAt > originalUpdatedAt);
    }
}
