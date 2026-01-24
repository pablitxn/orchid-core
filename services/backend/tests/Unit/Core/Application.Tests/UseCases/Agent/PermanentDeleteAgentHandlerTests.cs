using Application.Interfaces;
using Application.UseCases.Agent.PermanentDeleteAgent;
using Domain.Entities;
using Domain.Exceptions;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Agent;

public class PermanentDeleteAgentHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly PermanentDeleteAgentHandler _handler;

    public PermanentDeleteAgentHandlerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _handler = new PermanentDeleteAgentHandler(_agentRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Permanently_Delete_Agent_From_RecycleBin()
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
            IsInRecycleBin = true
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.DeleteAsync(agentId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new PermanentDeleteAgentCommand(agentId, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _agentRepositoryMock.Verify(x => x.DeleteAsync(agentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Throw_AgentNotFoundException_When_Agent_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentEntity?)null);

        var command = new PermanentDeleteAgentCommand(agentId, userId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AgentNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(agentId.ToString(), exception.Message);

        _agentRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var command = new PermanentDeleteAgentCommand(agentId, attemptingUserId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedDomainAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        _agentRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var command = new PermanentDeleteAgentCommand(agentId, userId);

        // Act & Assert
        await Assert.ThrowsAsync<AgentNotInRecycleBinException>(
            () => _handler.Handle(command, CancellationToken.None));

        _agentRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Not_Call_UpdateAsync()
    {
        // Arrange - permanent delete should call DeleteAsync, not UpdateAsync
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Test Agent",
            UserId = userId,
            IsDeleted = true,
            IsInRecycleBin = true
        };

        _agentRepositoryMock.Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(x => x.DeleteAsync(agentId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new PermanentDeleteAgentCommand(agentId, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _agentRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<AgentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _agentRepositoryMock.Verify(x => x.DeleteAsync(agentId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
