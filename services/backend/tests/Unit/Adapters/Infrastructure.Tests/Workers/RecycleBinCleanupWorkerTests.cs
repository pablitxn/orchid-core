using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Workers.RecycleBinCleanup;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Infrastructure.Tests.Workers;

public class RecycleBinCleanupWorkerTests
{
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<ILogger<RecycleBinCleanupWorker>> _loggerMock;
    private readonly IServiceProvider _serviceProvider;

    public RecycleBinCleanupWorkerTests()
    {
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _loggerMock = new Mock<ILogger<RecycleBinCleanupWorker>>();

        var services = new ServiceCollection();
        services.AddSingleton(_agentRepositoryMock.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CleanupExpiredAgents_Should_Delete_Expired_Agents()
    {
        // Arrange
        var expiredAgents = new List<AgentEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Expired Agent 1",
                IsDeleted = true,
                IsInRecycleBin = true,
                RecycleBinExpiresAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Expired Agent 2",
                IsDeleted = true,
                IsInRecycleBin = true,
                RecycleBinExpiresAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        _agentRepositoryMock.Setup(x => x.ListExpiredRecycleBinAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredAgents);

        _agentRepositoryMock.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new TestableRecycleBinCleanupWorker(_serviceProvider, _loggerMock.Object);

        // Act
        await worker.TestCleanupExpiredAgentsAsync(CancellationToken.None);

        // Assert
        _agentRepositoryMock.Verify(
            x => x.DeleteAsync(expiredAgents[0].Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _agentRepositoryMock.Verify(
            x => x.DeleteAsync(expiredAgents[1].Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupExpiredAgents_Should_Not_Delete_When_No_Expired_Agents()
    {
        // Arrange
        _agentRepositoryMock.Setup(x => x.ListExpiredRecycleBinAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity>());

        var worker = new TestableRecycleBinCleanupWorker(_serviceProvider, _loggerMock.Object);

        // Act
        await worker.TestCleanupExpiredAgentsAsync(CancellationToken.None);

        // Assert
        _agentRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredAgents_Should_Continue_When_One_Deletion_Fails()
    {
        // Arrange
        var agent1Id = Guid.NewGuid();
        var agent2Id = Guid.NewGuid();
        var agent3Id = Guid.NewGuid();

        var expiredAgents = new List<AgentEntity>
        {
            new() { Id = agent1Id, Name = "Agent 1", IsDeleted = true, IsInRecycleBin = true },
            new() { Id = agent2Id, Name = "Agent 2 (will fail)", IsDeleted = true, IsInRecycleBin = true },
            new() { Id = agent3Id, Name = "Agent 3", IsDeleted = true, IsInRecycleBin = true }
        };

        _agentRepositoryMock.Setup(x => x.ListExpiredRecycleBinAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredAgents);

        _agentRepositoryMock.Setup(x => x.DeleteAsync(agent1Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _agentRepositoryMock.Setup(x => x.DeleteAsync(agent2Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));
        _agentRepositoryMock.Setup(x => x.DeleteAsync(agent3Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new TestableRecycleBinCleanupWorker(_serviceProvider, _loggerMock.Object);

        // Act
        await worker.TestCleanupExpiredAgentsAsync(CancellationToken.None);

        // Assert - Agent 1 and 3 should still be deleted even though Agent 2 failed
        _agentRepositoryMock.Verify(x => x.DeleteAsync(agent1Id, It.IsAny<CancellationToken>()), Times.Once);
        _agentRepositoryMock.Verify(x => x.DeleteAsync(agent2Id, It.IsAny<CancellationToken>()), Times.Once);
        _agentRepositoryMock.Verify(x => x.DeleteAsync(agent3Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CleanupExpiredAgents_Should_Log_Information_About_Deleted_Agents()
    {
        // Arrange
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Agent",
            IsDeleted = true,
            IsInRecycleBin = true,
            RecycleBinExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        _agentRepositoryMock.Setup(x => x.ListExpiredRecycleBinAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity> { agent });

        _agentRepositoryMock.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new TestableRecycleBinCleanupWorker(_serviceProvider, _loggerMock.Object);

        // Act
        await worker.TestCleanupExpiredAgentsAsync(CancellationToken.None);

        // Assert - Verify logging was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Permanently deleted agent")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupExpiredAgents_Should_Log_Error_When_Deletion_Fails()
    {
        // Arrange
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Failing Agent",
            IsDeleted = true,
            IsInRecycleBin = true
        };

        _agentRepositoryMock.Setup(x => x.ListExpiredRecycleBinAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity> { agent });

        _agentRepositoryMock.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var worker = new TestableRecycleBinCleanupWorker(_serviceProvider, _loggerMock.Object);

        // Act
        await worker.TestCleanupExpiredAgentsAsync(CancellationToken.None);

        // Assert - Verify error logging was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to permanently delete")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test helper class to expose the private cleanup method for testing
    /// </summary>
    private class TestableRecycleBinCleanupWorker : RecycleBinCleanupWorker
    {
        public TestableRecycleBinCleanupWorker(
            IServiceProvider serviceProvider,
            ILogger<RecycleBinCleanupWorker> logger)
            : base(serviceProvider, logger)
        {
        }

        public async Task TestCleanupExpiredAgentsAsync(CancellationToken cancellationToken)
        {
            // Use reflection to call the private method
            var method = typeof(RecycleBinCleanupWorker)
                .GetMethod("CleanupExpiredAgentsAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            await (Task)method!.Invoke(this, new object[] { cancellationToken })!;
        }
    }
}
