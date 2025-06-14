using Application.Interfaces;
using Application.UseCases.Team.CreateTeam;
using Domain.Entities;
using Domain.Enums;
using Moq;

namespace Application.Tests.UseCases.Team;

public class CreateTeamHandlerTests
{
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly CreateTeamHandler _handler;
    private readonly Mock<ITeamRepository> _repo = new();

    public CreateTeamHandlerTests()
    {
        _handler = new CreateTeamHandler(_repo.Object, _agents.Object);
    }

    [Fact]
    public async Task Handle_CreatesTeam()
    {
        var cmd = new CreateTeamCommand(
            "Test Team",
            "desc",
            TeamInteractionPolicy.Open,
            [new TeamAgentInput(Guid.NewGuid(), "Lead", 0)]
        );

        _agents.Setup(a => a.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentEntity { Id = Guid.NewGuid(), Name = "A" });

        var result = await _handler.Handle(cmd, CancellationToken.None);

        _repo.Verify(r => r.CreateAsync(It.IsAny<TeamEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(cmd.Name, result.Name);
    }
}