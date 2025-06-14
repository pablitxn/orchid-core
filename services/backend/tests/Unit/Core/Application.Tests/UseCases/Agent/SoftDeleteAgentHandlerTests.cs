using Application.Interfaces;
using Application.UseCases.Agent.SoftDeleteAgent;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Agent;

public class SoftDeleteAgentHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly SoftDeleteAgentHandler _handler;

    public SoftDeleteAgentHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _handler = new SoftDeleteAgentHandler(_agentRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Soft_Delete_Agent_When_User_Is_Owner()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId, // User owns the agent
            IsPublic = false
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new SoftDeleteAgentCommand(agentId, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(agent.IsDeleted);
        Assert.True(agent.IsInRecycleBin);
        Assert.NotNull(agent.DeletedAt);
        Assert.NotNull(agent.RecycleBinExpiresAt);
        Assert.True(agent.RecycleBinExpiresAt > DateTime.UtcNow.AddDays(29)); // Should be ~30 days
        
        _agentRepositoryMock.Verify(x => x.UpdateAsync(agent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Throw_UnauthorizedAccessException_When_User_Is_Not_Owner()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var attemptingUserId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = ownerId, // Different owner
            IsPublic = false
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new SoftDeleteAgentCommand(agentId, attemptingUserId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
        
        Assert.Contains(attemptingUserId.ToString(), exception.Message);
        Assert.Contains(agentId.ToString(), exception.Message);
        
        _agentRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Throw_InvalidOperationException_When_Agent_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        
        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        var command = new SoftDeleteAgentCommand(agentId, userId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
        
        Assert.Contains(agentId.ToString(), exception.Message);
        
        _agentRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Update_UpdatedAt_Timestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var originalUpdatedAt = DateTime.UtcNow.AddDays(-1);
        
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            UpdatedAt = originalUpdatedAt
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new SoftDeleteAgentCommand(agentId, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(agent.UpdatedAt > originalUpdatedAt);
    }
}